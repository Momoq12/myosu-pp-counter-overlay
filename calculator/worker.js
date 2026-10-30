// SPDX-License-Identifier: LGPL-3.0-or-later
'use strict';
const fs = require('fs');
const readline = require('readline');
const rosu = require('./package/rosu_pp_js.js');
let map, gradual, identity = '', passed = 0, lastTime = -1;
readline.createInterface({ input: process.stdin, crlfDelay: Infinity }).on('line', line => {
  try {
    const s = JSON.parse(line);
    const total = s.n300 + s.n100 + s.n50 + s.nMiss + (s.mode === 3 ? s.nGeki + s.nKatu : 0);
    const key = s.path + '|' + s.mods + '|' + s.mode;
    if (key !== identity || total < passed || s.playTime < lastTime - 500) {
      if (gradual) gradual.free();
      if (map) map.free();
      map = new rosu.Beatmap(fs.readFileSync(s.path));
      if (map.mode !== s.mode) map.convert(s.mode, s.mods);
      const difficulty = new rosu.Difficulty({ mods: s.mods, lazer: false });
      gradual = difficulty.gradualPerformance(map);
      difficulty.free();
      identity = key; passed = 0;
    }
    lastTime = s.playTime;
    if (!total) { process.stdout.write(JSON.stringify({ pp: 0, stars: 0 }) + '\n'); return; }
    const state = { maxCombo: s.maxCombo, n300: s.n300, n100: s.n100, n50: s.n50,
      nGeki: s.nGeki, nKatu: s.nKatu, misses: s.nMiss, legacyTotalScore: s.score };
    // A slider break can change combo without a new judgement. Recompute that
    // partial play directly; otherwise advance the gradual calculator efficiently.
    let result;
    if (total > passed) { result = gradual.nth(state, total - passed - 1); passed = total; }
    else {
      const calc = new rosu.Performance({ mods: s.mods, lazer: false, passedObjects: total,
        combo: s.maxCombo, n300: s.n300, n100: s.n100, n50: s.n50,
        nGeki: s.nGeki, nKatu: s.nKatu, misses: s.nMiss, legacyTotalScore: s.score });
      result = calc.calculate(map); calc.free();
    }
    if (!result) throw Error('No PP result for this hit count');
    const difficulty = result.difficulty;
    const out = { pp: result.pp, stars: difficulty.stars };
    difficulty.free();
    result.free(); process.stdout.write(JSON.stringify(out) + '\n');
  } catch (e) { process.stdout.write(JSON.stringify({ error: String(e.message || e) }) + '\n'); }
});
