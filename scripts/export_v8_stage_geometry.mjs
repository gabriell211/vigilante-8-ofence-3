#!/usr/bin/env node
/** Export the ten original V8 stage XOBF object banks for inspection. */
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { exportArchive } from './export_v8_disc_geometry.mjs';

const stages = [
  ['AIRGRAVE', 'v8_aircraft_graveyard'],
  ['CANYNLND', 'v8_canyonlands'],
  ['CASNOCTY', 'v8_casino_city'],
  ['HOOVRDAM', 'v8_hoover_dam'],
  ['OILFIELD', 'v8_oil_fields'],
  ['SANDFACT', 'v8_sand_factory'],
  ['SCRTBASE', 'v8_secret_base'],
  ['SKIRESRT', 'v8_ski_resort'],
  ['VALLYFRM', 'v8_valley_farms'],
  ['WILDWEST', 'v8_ghost_town'],
];

export function exportStages(sourceRoot, destination) {
  fs.mkdirSync(destination, { recursive: true });
  const records = [];
  for (const [sourceName, stageId] of stages) {
    const source = path.join(sourceRoot, `${sourceName}.EXP`);
    if (!fs.existsSync(source)) throw new Error(`Missing source arena bank: ${source}`);
    const folder = path.join(destination, stageId);
    const archive = exportArchive(source, folder, 'v8');
    records.push({ stageId, sourceName, sourceSha256: archive.sourceSha256,
      objectBanks: archive.banks });
  }
  return { schemaVersion: 1, status: 'stage_object_geometry_only_not_playable',
    terrainReady: false, placementsReady: false, collisionReady: false,
    stages: records };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [sourceRoot, destination] = process.argv.slice(2);
  if (!sourceRoot || !destination) {
    console.error('Usage: node scripts/export_v8_stage_geometry.mjs <extracted TERRAIN directory> <output-directory>');
    process.exitCode = 2;
  } else {
    try {
      console.log(JSON.stringify(exportStages(sourceRoot, destination), null, 2));
    } catch (error) {
      console.error(error.message);
      process.exitCode = 1;
    }
  }
}
