#!/usr/bin/env node
/** Export source XOBF triangle groups as OBJ inspection geometry, without inventing vehicle identities. */
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const PACKET_SIZES = {
  v8: [12, 28, 20, 28, 12, 20, 12, 20, 16, 24, 12, 24, 20, 20, 0, 20],
  v82: [12, 32, 20, 32, 12, 24, 12, 24, 16, 28, 12, 28, 24, 24, 0, 24],
};
// Native COMMON.EXP bank order, corroborated by the archived retail vehicle matrix.
const V8_BODY_IDS = [
  'v8_rattler', 'v8_clydesdale', 'v8_strider', 'v8_jefferson', 'v8_van',
  'v8_moth_truck', 'v8_glenn_4x4', 'v8_palomino', 'v8_leprechaun',
  'v8_stag_pickup', 'v8_school_bus', 'v8_manta', 'v8_luxo_saucer',
];
// Native V8:2 vehicle order from Vehicle.cs and GameManager.vehicleConfigs.
const V82_BODY_IDS = [
  'v82_wonderwagon', 'v82_thunderbolt', 'v82_dakota_stunt_cycle',
  'v82_samson_tow_truck', 'v82_livingston_truck', 'v82_xanadu_rv',
  'v82_palomino_xiii', 'v82_el_guerrero', 'v82_blue_burro_bus',
  'v82_excelsior_stretch', 'v82_tsunami', 'v82_marathon',
  'v82_moon_trekker', 'v82_grubb_dual_loader', 'v82_chrono_stinger',
  'v82_vertigo', 'v82_goliath_halftrack', 'v82_wapiti_4wd',
];

function bounds(data, offset, length, label) {
  if (!Number.isSafeInteger(offset) || !Number.isSafeInteger(length) || offset < 0 || length < 0 || offset + length > data.length) {
    throw new Error(`${label} lies outside its source bank`);
  }
}

function iffForms(data) {
  const forms = [];
  function walk(start, end) {
    for (let cursor = start; cursor + 8 <= end;) {
      const tag = data.toString('ascii', cursor, cursor + 4);
      const size = data.readUInt32BE(cursor + 4);
      const body = cursor + 8;
      const next = body + size + (size & 1);
      if (!/^[\x20-\x7e]{4}$/.test(tag) || next > end) break;
      if (tag === 'FORM') {
        bounds(data, body, 4, 'FORM type');
        const type = data.toString('ascii', body, body + 4);
        if (type === 'XOBF') forms.push({ start: body + 4, end: body + size });
        walk(body + 4, body + size);
      }
      cursor = next;
    }
  }
  walk(0, data.length);
  return forms;
}

function findBin(data, form) {
  for (let cursor = form.start; cursor + 8 <= form.end;) {
    const tag = data.toString('ascii', cursor, cursor + 4);
    const size = data.readUInt32BE(cursor + 4);
    const body = cursor + 8;
    const next = body + size + (size & 1);
    if (next > form.end) throw new Error('Malformed XOBF chunk');
    if (tag === 'BIN ') return data.subarray(body, body + size);
    cursor = next;
  }
  throw new Error('XOBF has no BIN chunk');
}

export function decodeBankGeometry(bin, dialect = 'v8') {
  const packetSizes = PACKET_SIZES[dialect];
  if (!packetSizes) throw new Error(`Unsupported dialect: ${dialect}`);
  bounds(bin, 0, 28, 'BIN header');
  const groupCount = bin.readUInt32LE(0);
  const groupTable = bin.readUInt32LE(4);
  if (groupCount > 2048) throw new Error('Implausible render group count');
  bounds(bin, groupTable, groupCount * 4, 'render group table');
  const groups = [];
  for (let index = 0; index < groupCount; index++) {
    const descriptor = groupTable + bin.readUInt32LE(groupTable + index * 4);
    bounds(bin, descriptor, 28, `group ${index} descriptor`);
    const vertexCount = bin.readUInt32LE(descriptor);
    const vertexOffset = descriptor + bin.readUInt32LE(descriptor + 4);
    const polygonCount = bin.readUInt32LE(descriptor + 16);
    let polygonOffset = descriptor + bin.readUInt32LE(descriptor + 20);
    const scaleShift = bin[descriptor + 24];
    if (vertexCount > 65536 || polygonCount > 65536 || scaleShift > 15) {
      throw new Error(`Implausible geometry in group ${index}`);
    }
    bounds(bin, vertexOffset, vertexCount * 8, `group ${index} vertices`);
    const vertices = [];
    for (let vertex = 0; vertex < vertexCount; vertex++) {
      const offset = vertexOffset + vertex * 8;
      const scale = 1 << scaleShift;
      vertices.push([bin.readInt16LE(offset) / scale, -bin.readInt16LE(offset + 2) / scale || 0,
        bin.readInt16LE(offset + 4) / scale]);
    }
    const triangles = [];
    for (let polygon = 0; polygon < polygonCount; polygon++) {
      bounds(bin, polygonOffset, 12, `group ${index} polygon ${polygon}`);
      const kind = bin[polygonOffset + 3] & 15;
      let size = packetSizes[kind];
      if (kind === 10) size += bin.readUInt16LE(polygonOffset + 10) * (dialect === 'v82' ? 8 : 4);
      if (size === 0) throw new Error(`Unsupported packet kind ${kind} in group ${index}`);
      bounds(bin, polygonOffset, size, `group ${index} packet ${polygon}`);
      if (kind !== 10) {
        const face = [bin.readUInt16LE(polygonOffset + 4),
          bin.readUInt16LE(polygonOffset + 6), bin.readUInt16LE(polygonOffset + 8)];
        if (face.some(v => v >= vertexCount)) throw new Error(`Invalid vertex reference in group ${index}`);
        if (new Set(face).size === 3) triangles.push(face);
      }
      polygonOffset += size;
    }
    groups.push({ index, scaleShift, vertices, triangles });
  }
  return groups;
}

export function exportArchive(sourcePath, destination, dialect = 'v8') {
  const source = fs.readFileSync(sourcePath);
  const isV8Common = dialect === 'v8' && path.basename(sourcePath).toUpperCase() === 'COMMON.EXP';
  const isV82Common = dialect === 'v82' && path.basename(sourcePath).toUpperCase() === 'COMMON.EXP';
  const forms = iffForms(source);
  if (!forms.length) throw new Error('No XOBF banks in vehicle archive');
  fs.mkdirSync(destination, { recursive: true });
  const banks = [];
  for (let index = 0; index < forms.length; index++) {
    const bin = findBin(source, forms[index]);
    const groups = decodeBankGeometry(bin, dialect);
    const filename = `source_bank_${String(index).padStart(2, '0')}.obj`;
    const lines = [
      '# Authentic XOBF render geometry from the supplied PlayStation disc.',
      '# Groups are separate source parts; hierarchy, textures, animation and damage states are not assembled.',
      `o source_bank_${String(index).padStart(2, '0')}`,
    ];
    let vertexBase = 1;
    for (const group of groups) {
      lines.push(`g group_${String(group.index).padStart(3, '0')}`);
      for (const [x, y, z] of group.vertices) lines.push(`v ${x} ${y} ${z}`);
      for (const [a, b, c] of group.triangles) lines.push(`f ${vertexBase + a} ${vertexBase + b} ${vertexBase + c}`);
      vertexBase += group.vertices.length;
    }
    const objText = lines.join('\n') + '\n';
    fs.writeFileSync(path.join(destination, filename), objText);
    banks.push({ sourceIndex: index, obj: filename,
      ...(isV8Common ? { role: index < 13 ? 'vehicle_body' : index === 13 ? 'wheel_library' : 'shared_effects',
        ...(index < 13 ? { vehicleId: V8_BODY_IDS[index] } : {}) } : {}),
      ...(isV82Common ? { role: index < 18 ? 'vehicle_body' : index === 18 ? 'transform_library' : 'shared_bank',
        ...(index < 18 ? { vehicleId: V82_BODY_IDS[index] } : {}) } : {}),
      binSha256: crypto.createHash('sha256').update(bin).digest('hex'),
      objSha256: crypto.createHash('sha256').update(objText).digest('hex'),
      groups: groups.length, vertices: groups.reduce((n, g) => n + g.vertices.length, 0),
      triangles: groups.reduce((n, g) => n + g.triangles.length, 0) });
  }
  if (isV8Common && banks.length !== 16) throw new Error(`Expected 16 source banks in V8 COMMON.EXP, got ${banks.length}`);
  if (isV82Common && banks.length !== 20) throw new Error(`Expected 20 source banks in V8:2 COMMON.EXP, got ${banks.length}`);
  return { schemaVersion: 1, dialect, source: sourcePath.replaceAll('\\', '/'),
    sourceSha256: crypto.createHash('sha256').update(source).digest('hex'),
    status: 'source_geometry_only_not_playable',
    materialsReady: false, prefabReady: false, gameplayTested: false, banks };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [source, destination, dialect = 'v8'] = process.argv.slice(2);
  if (!source || !destination) {
    console.error('Usage: node scripts/export_v8_disc_geometry.mjs <VEHICLES.EXP> <output-directory> [v8|v82]');
    process.exitCode = 2;
  } else {
    try {
      console.log(JSON.stringify(exportArchive(source, destination, dialect), null, 2));
    } catch (error) {
      console.error(error.message);
      process.exitCode = 1;
    }
  }
}
