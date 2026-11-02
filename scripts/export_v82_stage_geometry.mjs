#!/usr/bin/env node
/** Export the eight original V8:2 stage XOBF object banks for inspection. */
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { exportArchive } from './export_v8_disc_geometry.mjs';

const stages = [
  ['BAYOU', 'v82_ghastly_bayou'],
  ['HARBOR', 'v82_pacific_harbor'],
  ['LAUNCH', 'v82_launch_site'],
  ['NUCLEAR', 'v82_nuclear_plant'],
  ['OILFIELD', 'v82_alaskan_pipeline'],
  ['OLYMPIC', 'v82_winter_games'],
  ['ROUTE66', 'v82_meteor_crater'],
  ['STEELMIL', 'v82_steel_mill'],
];

export function exportStages(sourceRoot, destination) {
  fs.mkdirSync(destination, { recursive: true });
  const records = [];
  for (const [sourceName, stageId] of stages) {
    const source = path.join(sourceRoot, `${sourceName}.EXP`);
    if (!fs.existsSync(source)) throw new Error(`Missing source arena bank: ${source}`);
    const folder = path.join(destination, stageId);
    const archive = exportArchive(source, folder, 'v82');
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
    console.error('Usage: node scripts/export_v82_stage_geometry.mjs <extracted LEVELS directory> <output-directory>');
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
