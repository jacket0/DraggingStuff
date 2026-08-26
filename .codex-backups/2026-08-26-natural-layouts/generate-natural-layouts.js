const fs = require('fs');

const levelConfigs = {
  2: { counts: [12, 12, 12, 12], activePairs: 3, activeTriples: 1, hiddenPairs: 3, hiddenTriples: 2 },
  3: { counts: [15, 15, 12, 12], activePairs: 4, activeTriples: 2, hiddenPairs: 4, hiddenTriples: 3 },
  4: { counts: [15, 15, 15, 15], activePairs: 6, activeTriples: 2, hiddenPairs: 6, hiddenTriples: 4 },
  5: { counts: [18, 18, 15, 15], activePairs: 7, activeTriples: 3, hiddenPairs: 7, hiddenTriples: 5 },
  6: { counts: [18, 18, 18, 18], activePairs: 9, activeTriples: 3, hiddenPairs: 7, hiddenTriples: 7 }
};

const adjacentShelves = [];
const distantShelves = [];
const shelfRuns = [];

for (let column = 0; column < 3; column++) {
  for (let row = 0; row < 3; row++)
    adjacentShelves.push([column * 4 + row, column * 4 + row + 1]);

  shelfRuns.push([column * 4, column * 4 + 1, column * 4 + 2]);
  shelfRuns.push([column * 4 + 1, column * 4 + 2, column * 4 + 3]);
  distantShelves.push([column * 4, column * 4 + 2]);
  distantShelves.push([column * 4, column * 4 + 3]);
  distantShelves.push([column * 4 + 1, column * 4 + 3]);
}

for (let row = 0; row < 4; row++) {
  adjacentShelves.push([row, row + 4]);
  adjacentShelves.push([row + 4, row + 8]);
  distantShelves.push([row, row + 8]);
  shelfRuns.push([row, row + 4, row + 8]);
}

function createRandom(seed) {
  let value = seed >>> 0;
  return () => {
    value += 0x6D2B79F5;
    let result = value;
    result = Math.imul(result ^ result >>> 15, result | 1);
    result ^= result + Math.imul(result ^ result >>> 7, result | 61);
    return ((result ^ result >>> 14) >>> 0) / 4294967296;
  };
}

function shuffle(values, random) {
  for (let index = values.length - 1; index > 0; index--) {
    const targetIndex = Math.floor(random() * (index + 1));
    [values[index], values[targetIndex]] = [values[targetIndex], values[index]];
  }

  return values;
}

function createOccupancies(config, random) {
  const occupancies = Array.from({ length: 12 }, () => [1, 1, 1]);
  const activeShelves = shuffle([...Array(12).keys()], random);

  for (const shelfIndex of activeShelves.slice(0, config.activeTriples))
    occupancies[shelfIndex][0] = 3;

  for (const shelfIndex of activeShelves.slice(config.activeTriples, config.activeTriples + config.activePairs))
    occupancies[shelfIndex][0] = 2;

  const hiddenLayers = [];
  for (let shelfIndex = 0; shelfIndex < 12; shelfIndex++) {
    hiddenLayers.push([shelfIndex, 1]);
    hiddenLayers.push([shelfIndex, 2]);
  }

  shuffle(hiddenLayers, random);

  for (let index = 0; index < config.hiddenTriples; index++) {
    const [shelfIndex, layerIndex] = hiddenLayers[index];
    occupancies[shelfIndex][layerIndex] = 3;
  }

  for (let index = config.hiddenTriples; index < config.hiddenTriples + config.hiddenPairs; index++) {
    const [shelfIndex, layerIndex] = hiddenLayers[index];
    occupancies[shelfIndex][layerIndex] = 2;
  }

  return occupancies;
}

function createLayout(config, random) {
  const occupancies = createOccupancies(config, random);
  const layout = Array.from({ length: 12 }, () => Array.from({ length: 3 }, () => [null, null, null]));
  const positions = [];

  for (let shelfIndex = 0; shelfIndex < 12; shelfIndex++) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      const slotIndices = shuffle([0, 1, 2], random).slice(0, occupancies[shelfIndex][layerIndex]);
      for (const slotIndex of slotIndices)
        positions.push([shelfIndex, layerIndex, slotIndex]);
    }
  }

  const items = [];
  for (let itemType = 0; itemType < config.counts.length; itemType++) {
    for (let count = 0; count < config.counts[itemType]; count++)
      items.push(itemType);
  }

  shuffle(items, random);

  for (let index = 0; index < positions.length; index++) {
    const [shelfIndex, layerIndex, slotIndex] = positions[index];
    layout[shelfIndex][layerIndex][slotIndex] = items[index];
  }

  return { layout, positions };
}

function layerItems(layout, shelfIndex, layerIndex) {
  return layout[shelfIndex][layerIndex].filter(itemType => itemType !== null);
}

function countType(items, itemType) {
  return items.filter(value => value === itemType).length;
}

function hasType(layout, shelfIndex, layerIndex, itemType) {
  return layout[shelfIndex][layerIndex].includes(itemType);
}

function scoreLayout(layout) {
  let score = 0;

  for (let shelfIndex = 0; shelfIndex < 12; shelfIndex++) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      const items = layerItems(layout, shelfIndex, layerIndex);

      if (items.length === 3 && items.every(itemType => itemType === items[0]))
        score += 10000;

      for (let itemType = 0; itemType < 4; itemType++) {
        if (countType(items, itemType) >= 2)
          score += layerIndex === 0 ? 120 : 12;
      }

      for (let slotIndex = 1; slotIndex < 3; slotIndex++) {
        if (layout[shelfIndex][layerIndex][slotIndex] !== null && layout[shelfIndex][layerIndex][slotIndex] === layout[shelfIndex][layerIndex][slotIndex - 1])
          score += 3;
      }
    }

    for (let layerIndex = 0; layerIndex < 2; layerIndex++) {
      const currentItems = layerItems(layout, shelfIndex, layerIndex);
      const nextItems = layerItems(layout, shelfIndex, layerIndex + 1);

      for (let itemType = 0; itemType < 4; itemType++) {
        if (currentItems.includes(itemType) && nextItems.includes(itemType))
          score += 9;

        if (countType(currentItems, itemType) >= 2 && countType(nextItems, itemType) >= 2)
          score += 45;
      }
    }

    for (let itemType = 0; itemType < 4; itemType++) {
      if ([0, 1, 2].every(layerIndex => hasType(layout, shelfIndex, layerIndex, itemType)))
        score += 45;
    }
  }

  for (const [firstShelf, secondShelf] of adjacentShelves) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      const firstItems = layerItems(layout, firstShelf, layerIndex);
      const secondItems = layerItems(layout, secondShelf, layerIndex);

      for (let itemType = 0; itemType < 4; itemType++) {
        if (firstItems.includes(itemType) && secondItems.includes(itemType))
          score += 7;

        if (countType(firstItems, itemType) >= 2 && countType(secondItems, itemType) >= 2)
          score += 60;
      }

      if (firstItems.length === secondItems.length && firstItems.slice().sort().join('') === secondItems.slice().sort().join(''))
        score += 20;

      for (let slotIndex = 0; slotIndex < 3; slotIndex++) {
        const firstItem = layout[firstShelf][layerIndex][slotIndex];
        if (firstItem !== null && firstItem === layout[secondShelf][layerIndex][slotIndex])
          score += 4;
      }
    }
  }

  for (const [firstShelf, secondShelf] of distantShelves) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      const firstItems = layerItems(layout, firstShelf, layerIndex);
      const secondItems = layerItems(layout, secondShelf, layerIndex);

      for (let itemType = 0; itemType < 4; itemType++) {
        if (firstItems.includes(itemType) && secondItems.includes(itemType))
          score += 3;
      }

      if (firstItems.length === secondItems.length && firstItems.slice().sort().join('') === secondItems.slice().sort().join(''))
        score += 15;

      for (let slotIndex = 0; slotIndex < 3; slotIndex++) {
        const firstItem = layout[firstShelf][layerIndex][slotIndex];
        if (firstItem !== null && firstItem === layout[secondShelf][layerIndex][slotIndex])
          score += 2;
      }
    }
  }

  for (const run of shelfRuns) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      for (let itemType = 0; itemType < 4; itemType++) {
        if (run.every(shelfIndex => hasType(layout, shelfIndex, layerIndex, itemType)))
          score += 80;
      }
    }
  }

  return score;
}

function optimizeLayout(candidate, random) {
  let currentScore = scoreLayout(candidate.layout);
  let bestScore = currentScore;
  let bestLayout = structuredClone(candidate.layout);
  const iterations = 40000;

  for (let iteration = 0; iteration < iterations; iteration++) {
    const firstPosition = candidate.positions[Math.floor(random() * candidate.positions.length)];
    const secondPosition = candidate.positions[Math.floor(random() * candidate.positions.length)];
    const firstValue = candidate.layout[firstPosition[0]][firstPosition[1]][firstPosition[2]];
    const secondValue = candidate.layout[secondPosition[0]][secondPosition[1]][secondPosition[2]];

    if (firstValue === secondValue)
      continue;

    candidate.layout[firstPosition[0]][firstPosition[1]][firstPosition[2]] = secondValue;
    candidate.layout[secondPosition[0]][secondPosition[1]][secondPosition[2]] = firstValue;

    const nextScore = scoreLayout(candidate.layout);
    const temperature = Math.max(0.05, 8 * (1 - iteration / iterations));
    const accepted = nextScore <= currentScore || random() < Math.exp((currentScore - nextScore) / temperature);

    if (accepted) {
      currentScore = nextScore;
      if (nextScore < bestScore) {
        bestScore = nextScore;
        bestLayout = structuredClone(candidate.layout);
      }
    } else {
      candidate.layout[firstPosition[0]][firstPosition[1]][firstPosition[2]] = firstValue;
      candidate.layout[secondPosition[0]][secondPosition[1]][secondPosition[2]] = secondValue;
    }
  }

  return { layout: bestLayout, score: bestScore };
}

function createState(layout) {
  return layout.map(shelf => shelf.map(layer => layer.filter(itemType => itemType !== null)));
}

function isMatch(layer) {
  return layer.length === 3 && layer.every(itemType => itemType === layer[0]);
}

function normalizeState(shelves) {
  let automaticMatches = 0;

  for (const shelf of shelves) {
    while (shelf.length > 0) {
      if (isMatch(shelf[0])) {
        shelf[0].length = 0;
        automaticMatches++;
      }

      if (shelf[0].length > 0)
        break;

      shelf.shift();
    }
  }

  return automaticMatches;
}

function stateKey(shelves) {
  return shelves
    .map(shelf => shelf.map(layer => layer.slice().sort().join('')).join('/'))
    .sort()
    .join('|');
}

function cloneState(shelves) {
  return shelves.map(shelf => shelf.map(layer => layer.slice()));
}

function getMoves(shelves) {
  const moves = [];

  for (let sourceShelf = 0; sourceShelf < shelves.length; sourceShelf++) {
    if (shelves[sourceShelf].length === 0)
      continue;

    const sourceLayer = shelves[sourceShelf][0];

    for (const itemType of new Set(sourceLayer)) {
      for (let targetShelf = 0; targetShelf < shelves.length; targetShelf++) {
        if (sourceShelf === targetShelf || shelves[targetShelf].length === 0)
          continue;

        const targetLayer = shelves[targetShelf][0];
        if (targetLayer.length >= 3)
          continue;

        const matchingItems = countType(targetLayer, itemType);
        const createsMatch = targetLayer.length === 2 && matchingItems === 2;
        const clearsSource = sourceLayer.length === 1;
        const mixesEmptyTarget = targetLayer.length === 0;
        const priority = (createsMatch ? 1000 : 0) + matchingItems * 80 + (clearsSource ? 45 : 0) + targetLayer.length * 8 - (mixesEmptyTarget ? 10 : 0);
        moves.push({ sourceShelf, targetShelf, itemType, priority });
      }
    }
  }

  return moves.sort((first, second) => second.priority - first.priority);
}

function solveLayout(layout, maxVisited = 180000) {
  const initialState = createState(layout);
  const visited = new Set();
  let stopped = false;

  function search(shelves, path, automaticMatchCount) {
    automaticMatchCount += normalizeState(shelves);

    if (shelves.every(shelf => shelf.length === 0))
      return { path, automaticMatchCount };

    if (visited.size >= maxVisited) {
      stopped = true;
      return null;
    }

    const key = stateKey(shelves);
    if (visited.has(key))
      return null;

    visited.add(key);

    for (const move of getMoves(shelves)) {
      const nextState = cloneState(shelves);
      const sourceLayer = nextState[move.sourceShelf][0];
      const targetLayer = nextState[move.targetShelf][0];
      sourceLayer.splice(sourceLayer.indexOf(move.itemType), 1);
      targetLayer.push(move.itemType);

      const createsMatch = isMatch(targetLayer);
      if (createsMatch)
        targetLayer.length = 0;

      const result = search(nextState, [...path, { ...move, createsMatch }], automaticMatchCount);
      if (result)
        return result;

      if (stopped)
        return null;
    }

    return null;
  }

  const result = search(initialState, [], 0);
  return { result, visited: visited.size };
}

function countAestheticViolations(layout) {
  let adjacentDoublePairs = 0;
  let threeShelfRuns = 0;
  let threeLayerRuns = 0;

  for (const [firstShelf, secondShelf] of adjacentShelves) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      for (let itemType = 0; itemType < 4; itemType++) {
        if (countType(layerItems(layout, firstShelf, layerIndex), itemType) >= 2 && countType(layerItems(layout, secondShelf, layerIndex), itemType) >= 2)
          adjacentDoublePairs++;
      }
    }
  }

  for (const run of shelfRuns) {
    for (let layerIndex = 0; layerIndex < 3; layerIndex++) {
      for (let itemType = 0; itemType < 4; itemType++) {
        if (run.every(shelfIndex => hasType(layout, shelfIndex, layerIndex, itemType)))
          threeShelfRuns++;
      }
    }
  }

  for (let shelfIndex = 0; shelfIndex < 12; shelfIndex++) {
    for (let itemType = 0; itemType < 4; itemType++) {
      if ([0, 1, 2].every(layerIndex => hasType(layout, shelfIndex, layerIndex, itemType)))
        threeLayerRuns++;
    }
  }

  return { adjacentDoublePairs, threeShelfRuns, threeLayerRuns };
}

function generateLevel(levelNumber, config) {
  let best = null;

  for (let attempt = 0; attempt < 20; attempt++) {
    const random = createRandom(levelNumber * 100000 + attempt * 7919 + 20260826);
    const optimized = optimizeLayout(createLayout(config, random), random);
    const violations = countAestheticViolations(optimized.layout);

    if (violations.adjacentDoublePairs > 0 || violations.threeShelfRuns > 0 || violations.threeLayerRuns > 0)
      continue;

    const solution = solveLayout(optimized.layout);
    if (!solution.result)
      continue;

    const matchMoves = solution.result.path.filter(move => move.createsMatch).length;
    const maximumMoves = config.counts.reduce((total, count) => total + count, 0) / 3 * 2.75;

    if (solution.result.path.length > maximumMoves)
      continue;

    const quality = optimized.score + solution.result.path.length * 1.5 - solution.result.automaticMatchCount * 3;

    if (best === null || quality < best.quality) {
      best = {
        levelNumber,
        layout: optimized.layout,
        score: optimized.score,
        quality,
        solutionMoves: solution.result.path.length,
        matchMoves,
        automaticMatches: solution.result.automaticMatchCount,
        visited: solution.visited,
        violations
      };
    }
  }

  if (best === null)
    throw new Error(`No solvable layout for level ${levelNumber}`);

  return best;
}

const results = [];

for (const [levelNumber, config] of Object.entries(levelConfigs)) {
  const result = generateLevel(Number(levelNumber), config);
  results.push(result);
  process.stdout.write(`Level ${levelNumber}: score=${result.score}, moves=${result.solutionMoves}, auto=${result.automaticMatches}, visited=${result.visited}, violations=${JSON.stringify(result.violations)}\n`);
}

fs.writeFileSync('Temp/natural-layouts.json', JSON.stringify(results, null, 2));
