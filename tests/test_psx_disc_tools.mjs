import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { inspectDisc, extractDiscFiles } from '../scripts/inspect_psx_disc.mjs';
import { decodeBankGeometry } from '../scripts/export_v8_disc_geometry.mjs';

const RAW = 2352;
const DATA = 2048;

function dirRecord(name, extent, size, directory = false) {
  const label = typeof name === 'number' ? Buffer.from([name]) : Buffer.from(name, 'ascii');
  const length = 33 + label.length + (label.length % 2 === 0 ? 1 : 0);
  const record = Buffer.alloc(length);
  record[0] = length;
  record.writeUInt32LE(extent, 2);
  record.writeUInt32BE(extent, 6);
  record.writeUInt32LE(size, 10);
  record.writeUInt32BE(size, 14);
  record[25] = directory ? 2 : 0;
  record[28] = 1;
  record[31] = 1;
  record[32] = label.length;
  label.copy(record, 33);
  return record;
}

function fixture(fileName = 'TEST.DAT;1') {
  const raw = Buffer.alloc(32 * RAW);
  function sector(lba, payload) { payload.copy(raw, lba * RAW + 24); }
  const pvd = Buffer.alloc(DATA);
  pvd[0] = 1;
  pvd.write('CD001', 1, 'ascii');
  pvd[6] = 1;
  pvd.write('TEST_VOLUME', 40, 'ascii');
  dirRecord(0, 20, DATA, true).copy(pvd, 156);
  sector(16, pvd);
  const root = Buffer.alloc(DATA);
  let position = 0;
  for (const record of [dirRecord(0, 20, DATA, true), dirRecord(1, 20, DATA, true), dirRecord(fileName, 21, 4)]) {
    record.copy(root, position);
    position += record.length;
  }
  sector(20, root);
  sector(21, Buffer.from('GAME'));
  return raw;
}

test('reads and extracts a Mode 2 ISO9660 file with its real length', t => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'v8-iso-test-'));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const image = path.join(dir, 'track.bin');
  fs.writeFileSync(image, fixture());
  const report = inspectDisc(image);
  assert.equal(report.volume, 'TEST_VOLUME');
  assert.equal(report.dataOffset, 24);
  assert.equal(report.files, 1);
  assert.equal(report.entries[0].path, 'TEST.DAT');
  assert.deepEqual(extractDiscFiles(image, report, path.join(dir, 'out')),
    { extracted: 1, skippedStreams: 0, skippedOutsideDataTrack: 0 });
  assert.equal(fs.readFileSync(path.join(dir, 'out', 'TEST.DAT'), 'ascii'), 'GAME');
});

test('rejects unsafe names in the disc directory', t => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'v8-iso-test-'));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const image = path.join(dir, 'track.bin');
  fs.writeFileSync(image, fixture('BAD/FILE;1'));
  assert.throws(() => inspectDisc(image), /Unsafe ISO9660 name/);
});

test('decodes source XOBF vertices and triangle packet with fixed-point scale', () => {
  const bin = Buffer.alloc(0x70);
  bin.writeUInt32LE(1, 0);
  bin.writeUInt32LE(0x20, 4);
  bin.writeUInt32LE(0x10, 0x20);
  bin.writeUInt32LE(3, 0x30);
  bin.writeUInt32LE(0x1c, 0x34);
  bin.writeUInt32LE(1, 0x40);
  bin.writeUInt32LE(0x34, 0x44);
  bin[0x48] = 8;
  bin.writeInt16LE(256, 0x4c);
  bin.writeInt16LE(-256, 0x54 + 2);
  bin.writeInt16LE(256, 0x5c + 4);
  bin.writeUInt16LE(0, 0x68);
  bin.writeUInt16LE(1, 0x6a);
  bin.writeUInt16LE(2, 0x6c);
  const groups = decodeBankGeometry(bin);
  assert.equal(groups.length, 1);
  assert.deepEqual(groups[0].vertices, [[1, 0, 0], [0, 1, 0], [0, 0, 1]]);
  assert.deepEqual(groups[0].triangles, [[0, 1, 2]]);
});

test('all committed classic vehicle meshes match the catalog and manifest', () => {
  const root = file => new URL(`../${file}`, import.meta.url);
  const roster = JSON.parse(fs.readFileSync(root('Assets/Resources/Catalog/roster.json'), 'utf8'));
  const manifest = JSON.parse(fs.readFileSync(root('Assets/GameContent/ExtractedV8/manifest.json'), 'utf8'));
  assert.equal(manifest.status, 'source_geometry_only_not_playable');
  assert.equal(manifest.prefabReady, false);
  const vehicles = manifest.banks.filter(bank => bank.role === 'vehicle_body');
  assert.equal(vehicles.length, 13);
  assert.deepEqual(new Set(vehicles.map(bank => bank.vehicleId)),
    new Set(roster.vehicles.filter(vehicle => vehicle.game === 'v8').map(vehicle => vehicle.id)));
  for (const bank of manifest.banks) {
    const obj = fs.readFileSync(root(`Assets/GameContent/ExtractedV8/${bank.obj}`), 'utf8');
    assert.equal(crypto.createHash('sha256').update(obj).digest('hex'), bank.objSha256);
    const vertices = obj.match(/^v /gm) ?? [];
    const faces = obj.match(/^f /gm) ?? [];
    assert.equal(vertices.length, bank.vertices, bank.obj);
    assert.equal(faces.length, bank.triangles, bank.obj);
    for (const line of obj.match(/^f .+$/gm) ?? []) {
      const indices = line.slice(2).split(' ').map(Number);
      assert.equal(indices.length, 3);
      assert(indices.every(index => index >= 1 && index <= bank.vertices), bank.obj);
    }
  }
});

test('all ten classic arena object banks match the catalog and contain real faces', () => {
  const root = file => new URL(`../${file}`, import.meta.url);
  const roster = JSON.parse(fs.readFileSync(root('Assets/Resources/Catalog/roster.json'), 'utf8'));
  const manifest = JSON.parse(fs.readFileSync(root('Assets/GameContent/ExtractedV8Stages/manifest.json'), 'utf8'));
  assert.equal(manifest.status, 'stage_object_geometry_only_not_playable');
  assert.equal(manifest.terrainReady, false);
  assert.equal(manifest.stages.length, 10);
  assert.deepEqual(new Set(manifest.stages.map(stage => stage.stageId)),
    new Set(roster.arenas.filter(stage => stage.game === 'v8' && !stage.bonus).map(stage => stage.id)));
  for (const stage of manifest.stages) {
    assert(stage.objectBanks.length > 0, stage.stageId);
    for (const bank of stage.objectBanks) {
      const obj = fs.readFileSync(root(`Assets/GameContent/ExtractedV8Stages/${stage.stageId}/${bank.obj}`), 'utf8');
      assert.equal(crypto.createHash('sha256').update(obj).digest('hex'), bank.objSha256);
      assert.equal((obj.match(/^v /gm) ?? []).length, bank.vertices);
      assert.equal((obj.match(/^f /gm) ?? []).length, bank.triangles);
      assert(bank.triangles > 0, stage.stageId);
    }
  }
});

test('European sequel disc contains all eighteen catalog vehicle bodies', () => {
  const root = file => new URL(`../${file}`, import.meta.url);
  const roster = JSON.parse(fs.readFileSync(root('Assets/Resources/Catalog/roster.json'), 'utf8'));
  const manifest = JSON.parse(fs.readFileSync(root('Assets/GameContent/ExtractedV82Disc/manifest.json'), 'utf8'));
  const vehicles = manifest.banks.filter(bank => bank.role === 'vehicle_body');
  assert.equal(manifest.status, 'source_geometry_only_not_playable');
  assert.equal(manifest.prefabReady, false);
  assert.equal(vehicles.length, 18);
  assert.deepEqual(new Set(vehicles.map(bank => bank.vehicleId)),
    new Set(roster.vehicles.filter(vehicle => vehicle.game === 'v82').map(vehicle => vehicle.id)));
  for (const bank of manifest.banks) {
    const obj = fs.readFileSync(root(`Assets/GameContent/ExtractedV82Disc/${bank.obj}`), 'utf8');
    assert.equal(crypto.createHash('sha256').update(obj).digest('hex'), bank.objSha256);
    assert.equal((obj.match(/^v /gm) ?? []).length, bank.vertices);
    assert.equal((obj.match(/^f /gm) ?? []).length, bank.triangles);
  }
});

test('European sequel disc includes object banks for all eight sequel arenas', () => {
  const root = file => new URL(`../${file}`, import.meta.url);
  const roster = JSON.parse(fs.readFileSync(root('Assets/Resources/Catalog/roster.json'), 'utf8'));
  const manifest = JSON.parse(fs.readFileSync(root('Assets/GameContent/ExtractedV82DiscStages/manifest.json'), 'utf8'));
  const legacy = JSON.parse(fs.readFileSync(root('Assets/GameContent/ExtractedStageObjectsV82/manifest.json'), 'utf8'));
  assert.equal(manifest.terrainReady, false);
  assert.equal(manifest.stages.length, 8);
  assert.deepEqual(new Set(manifest.stages.map(stage => stage.stageId)),
    new Set(roster.arenas.filter(stage => stage.game === 'v82').map(stage => stage.id)));
  for (const stage of manifest.stages) {
    assert(stage.objectBanks.length > 0);
    const earlier = legacy.stages.find(row => row.stageId === stage.stageId);
    assert.equal(stage.objectBanks[0].triangles, earlier.triangles);
    for (const bank of stage.objectBanks) {
      const obj = fs.readFileSync(root(`Assets/GameContent/ExtractedV82DiscStages/${stage.stageId}/${bank.obj}`), 'utf8');
      assert.equal(crypto.createHash('sha256').update(obj).digest('hex'), bank.objSha256);
      assert.equal((obj.match(/^f /gm) ?? []).length, bank.triangles);
    }
  }
});
