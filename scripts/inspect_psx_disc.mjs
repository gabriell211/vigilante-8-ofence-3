#!/usr/bin/env node
/** Inspect a PlayStation raw-sector data track without external dependencies. */
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const RAW_SECTOR = 2352;
const DATA_SECTOR = 2048;

export function inspectDisc(trackPath) {
  const fd = fs.openSync(trackPath, 'r');
  try {
    const bytes = fs.fstatSync(fd).size;
    if (bytes % RAW_SECTOR !== 0) throw new Error('Track size is not a multiple of 2352 bytes');
    const sectors = bytes / RAW_SECTOR;
    let dataOffset;
    for (const candidate of [16, 24]) {
      const descriptor = readSector(fd, sectors, 16, candidate);
      if (descriptor[0] === 1 && descriptor.toString('ascii', 1, 6) === 'CD001') {
        dataOffset = candidate;
        break;
      }
    }
    if (dataOffset === undefined) throw new Error('ISO9660 primary volume descriptor not found at sector 16');
    const descriptor = readSector(fd, sectors, 16, dataOffset);
    const volume = descriptor.toString('ascii', 40, 72).replace(/[\x00 ]+$/, '');
    const root = recordAt(descriptor, 156);
    if (!root?.directory) throw new Error('Invalid ISO9660 root directory');
    const entries = [];
    const visited = new Set();
    function walk(directory, prefix, depth) {
      if (depth > 32) throw new Error('Directory nesting exceeds 32 levels');
      const key = `${directory.extent}:${directory.size}`;
      if (visited.has(key)) throw new Error(`Repeated directory extent: ${key}`);
      visited.add(key);
      const data = readExtent(fd, sectors, dataOffset, directory.extent, directory.size);
      for (let position = 0; position < data.length;) {
        const length = data[position];
        if (length === 0) {
          position = (Math.floor(position / DATA_SECTOR) + 1) * DATA_SECTOR;
          continue;
        }
        const entry = recordAt(data, position);
        if (!entry) throw new Error(`Invalid directory record at ${prefix || '/'}:${position}`);
        position += length;
        if (entry.name === '.' || entry.name === '..') continue;
        if (!/^[\x20-\x7e]+$/.test(entry.name) || entry.name === '..' || entry.name.includes('/') || entry.name.includes('\\')) {
          throw new Error(`Unsafe ISO9660 name: ${JSON.stringify(entry.name)}`);
        }
        const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name;
        const inDataTrack = entry.extent + Math.ceil(entry.size / DATA_SECTOR) <= sectors;
        entries.push({ path: relativePath, directory: entry.directory, extent: entry.extent,
          size: entry.size, inDataTrack });
        if (entry.directory) {
          if (!inDataTrack) throw new Error(`Directory outside data track: ${relativePath}`);
          walk(entry, relativePath, depth + 1);
        }
      }
    }
    walk(root, '', 0);
    return { track: trackPath, volume, rawSectorSize: RAW_SECTOR, dataOffset,
      sectors, entries, files: entries.filter(e => !e.directory).length,
      directories: entries.filter(e => e.directory).length,
      outOfTrackFiles: entries.filter(e => !e.directory && !e.inDataTrack).length };
  } finally {
    fs.closeSync(fd);
  }
}

/** Extract ordinary ISO files. XA and STR sectors need their own decoder. */
export function extractDiscFiles(trackPath, report, destination) {
  const root = path.resolve(destination);
  fs.mkdirSync(root, { recursive: true });
  const fd = fs.openSync(trackPath, 'r');
  let extracted = 0;
  let skippedStreams = 0;
  try {
    for (const entry of report.entries) {
      if (entry.directory || !entry.inDataTrack) continue;
      if (/\.(XA|STR)$/i.test(entry.path)) {
        skippedStreams++;
        continue;
      }
      const target = path.resolve(root, ...entry.path.split('/'));
      if (!target.startsWith(root + path.sep)) throw new Error(`Unsafe extraction path: ${entry.path}`);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      const output = fs.openSync(target, 'w');
      try {
        for (let i = 0; i < Math.ceil(entry.size / DATA_SECTOR); i++) {
          const sector = readSector(fd, report.sectors, entry.extent + i, report.dataOffset);
          const count = Math.min(DATA_SECTOR, entry.size - i * DATA_SECTOR);
          fs.writeSync(output, sector, 0, count);
        }
      } finally {
        fs.closeSync(output);
      }
      extracted++;
    }
  } finally {
    fs.closeSync(fd);
  }
  return { extracted, skippedStreams, skippedOutsideDataTrack: report.outOfTrackFiles };
}

function readSector(fd, sectors, lba, offset) {
  if (lba < 0 || lba >= sectors) throw new Error(`Sector ${lba} outside track`);
  const result = Buffer.alloc(DATA_SECTOR);
  if (fs.readSync(fd, result, 0, DATA_SECTOR, lba * RAW_SECTOR + offset) !== DATA_SECTOR) {
    throw new Error(`Short read at sector ${lba}`);
  }
  return result;
}

function readExtent(fd, sectors, offset, extent, size) {
  if (size < 0 || extent < 0 || extent + Math.ceil(size / DATA_SECTOR) > sectors) {
    throw new Error(`Extent ${extent}/${size} outside data track`);
  }
  const result = Buffer.alloc(size);
  for (let i = 0; i < Math.ceil(size / DATA_SECTOR); i++) {
    const sector = readSector(fd, sectors, extent + i, offset);
    sector.copy(result, i * DATA_SECTOR, 0, Math.min(DATA_SECTOR, size - i * DATA_SECTOR));
  }
  return result;
}

function recordAt(buffer, position) {
  const length = buffer[position];
  if (length < 34 || position + length > buffer.length) return null;
  const nameLength = buffer[position + 32];
  if (33 + nameLength > length) return null;
  const nameBytes = buffer.subarray(position + 33, position + 33 + nameLength);
  let name = nameBytes.length === 1 && nameBytes[0] === 0 ? '.' :
    nameBytes.length === 1 && nameBytes[0] === 1 ? '..' : nameBytes.toString('ascii').replace(/;\d+$/, '');
  return { name, extent: buffer.readUInt32LE(position + 2),
    size: buffer.readUInt32LE(position + 10), directory: Boolean(buffer[position + 25] & 2) };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const track = process.argv[2];
  if (!track) {
    console.error('Usage: node scripts/inspect_psx_disc.mjs <Track 01.bin> [--json] [--extract <private-directory>]');
    process.exitCode = 2;
  } else {
    try {
      const report = inspectDisc(track);
      const extractIndex = process.argv.indexOf('--extract');
      if (extractIndex !== -1) {
        const destination = process.argv[extractIndex + 1];
        if (!destination || destination.startsWith('--')) throw new Error('--extract requires a directory');
        console.error(JSON.stringify(extractDiscFiles(track, report, destination)));
      }
      if (process.argv.includes('--json')) console.log(JSON.stringify(report, null, 2));
      else {
        console.log(`${report.volume}: ${report.files} files, ${report.directories} directories, ${report.outOfTrackFiles} outside the data track`);
        for (const entry of report.entries) {
          console.log(`${entry.directory ? 'DIR ' : entry.inDataTrack ? 'FILE' : 'OTHER'} ${entry.path} ${entry.size}`);
        }
      }
    } catch (error) {
      console.error(error.message);
      process.exitCode = 1;
    }
  }
}
