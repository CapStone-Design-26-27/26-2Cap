// Offline data authoring only. This script does not run in Unity.
const assert = require('node:assert/strict');

const scales = [0.5, 0.75, 1, 2, 2.35, 2.75, 3.25, 3.75, 4, 4.25];
const radius = level => 1.695 * scales[level];
const round = value => Math.round(value * 10000) / 10000;
const bits = mass => {
  const levels = [];
  for (let level = 0; mass > 0; level++, mass = Math.floor(mass / 2))
    if (mass % 2) levels.push(level);
  return levels;
};

// Ignore queue order and uniform mass scaling, and respect each map's tree symmetry.
function logicalSignature(stage) {
  const count = stage.mapId + 1;
  const initial = Array(count).fill(0);
  stage.balls.forEach(ball => { initial[ball.basketIndex] += 2 ** ball.level; });
  const queue = stage.playerBallQueue.map(level => 2 ** level).sort((a, b) => a - b);
  const divisor = [...initial, ...queue].filter(Boolean).reduce((a, b) => {
    while (b) [a, b] = [b, a % b];
    return a;
  });
  const orders = count === 2 ? [[0, 1], [1, 0]] : count === 3
    ? [[0, 1, 2], [1, 0, 2]]
    : [[0, 1, 2, 3], [1, 0, 2, 3], [0, 1, 3, 2], [1, 0, 3, 2],
      [2, 3, 0, 1], [3, 2, 0, 1], [2, 3, 1, 0], [3, 2, 1, 0]];
  return orders.map(order => JSON.stringify([stage.mapId,
    order.map(i => initial[i] / divisor), queue.map(mass => mass / divisor)])).sort()[0];
}

// Count allocations by animal kind, rather than permutations of identical balls.
function allocationCount(initial, target, queue, limit = Infinity) {
  const counts = new Map();
  queue.forEach(level => counts.set(level, (counts.get(level) || 0) + 1));
  const kinds = [...counts].sort((a, b) => b[0] - a[0]);
  const deficits = target.map((mass, i) => mass - initial[i]);
  let solutions = 0;
  function visit(kind) {
    if (solutions >= limit) return;
    if (kind === kinds.length) {
      if (deficits.every(mass => mass === 0)) solutions++;
      return;
    }
    const [level, count] = kinds[kind];
    const mass = 2 ** level;
    function distribute(basket, remaining) {
      if (basket === deficits.length - 1) {
        if (remaining * mass > deficits[basket]) return;
        deficits[basket] -= remaining * mass;
        visit(kind + 1);
        deficits[basket] += remaining * mass;
        return;
      }
      for (let n = 0; n <= Math.min(remaining, Math.floor(deficits[basket] / mass)); n++) {
        deficits[basket] -= n * mass;
        distribute(basket + 1, remaining - n);
        deficits[basket] += n * mass;
      }
    }
    distribute(0, count);
  }
  visit(0);
  return solutions;
}

function expandCampaign(stages, maps) {
  let seed = 261108;
  const random = max => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    return Math.floor(seed / 0x100000000 * max);
  };
  const signatures = new Set(stages.map(logicalSignature));
  const themes = [
    ['큰 공의 목적지', '전체 지급 공을 보고 큰 공의 목적지를 먼저 정한다.'],
    ['작은 부족분', '작은 공이 필요한 바구니를 구분하고 부족분을 채운다.'],
    ['전체 배분 계획', '현재 기울기만 따르지 않고 전체 공의 배분을 계획한다.'],
    ['서로 다른 조합', '같은 목표를 서로 다른 공의 조합으로 채운다.'],
    ['남은 공의 역할', '바구니를 채운 뒤 남을 공까지 고려해 배분한다.'],
    ['계층 균형 계획', '하위 바구니와 전체 저울의 목표를 함께 맞춘다.'],
    ['정밀한 배분', '가능한 공 종류별 배분 조합이 적은 문제를 해결한다.'],
  ];
  for (let stage = 31; stage <= 100; stage++) {
    const chapter = Math.floor((stage - 31) / 10);
    const within = (stage - 1) % 10;
    const mapId = stage === 100 ? 3 : chapter === 0 ? 1 + (within % 2) : chapter === 1 ? 2 + (within % 2)
      : chapter === 2 || chapter === 4 ? 3 : chapter === 3 ? 1 + (within % 2)
        : 1 + ((stage - 1) % 3);
    const ratios = maps.find(map => map.mapId === mapId).targetWeightRatios;
    const n = ratios.length;
    const breather = within < 2;
    const maxPatterns = breather ? 6 : chapter < 2 ? 4 : chapter < 4 ? 3 : 2;
    let selected;
    for (let attempt = 0; attempt < 60000; attempt++) {
      const unit = 6 + random(chapter < 2 ? 9 : 17);
      const target = ratios.map(ratio => ratio * unit);
      const initial = target.map(mass => 1 + random(Math.min(mass - 1, 30)));
      const initialLevels = initial.map(bits);
      if (initialLevels.some(levels => levels.reduce((sum, level) => sum + 2 * radius(level), 0)
        + (levels.length - 1) * 0.12 > 22)) continue;
      let drops = [];
      target.forEach((mass, basket) => bits(mass - initial[basket]).forEach(level => drops.push({ level, basket })));
      const desired = (breather ? 4 : chapter < 2 ? 5 : 6) + random(3);
      if (drops.length > desired) continue;
      while (drops.length < desired) {
        const choices = drops.map((drop, i) => drop.level > 0 ? i : -1).filter(i => i >= 0);
        if (!choices.length) break;
        const i = choices[random(choices.length)];
        const { level, basket } = drops[i];
        drops.splice(i, 1, { level: level - 1, basket }, { level: level - 1, basket });
      }
      if (drops.length !== desired || drops.some(drop => drop.level > 3)
        || new Set(drops.map(drop => drop.level)).size < 2) continue;
      for (let i = drops.length - 1; i > 0; i--) {
        const j = random(i + 1);
        [drops[i], drops[j]] = [drops[j], drops[i]];
      }
      const queue = drops.map(drop => drop.level);
      const count = allocationCount(initial, target, queue, maxPatterns + 1);
      if (count < 1 || count > maxPatterns) continue;
      const greedy = [...initial];
      queue.forEach(level => {
        let basket = 0;
        for (let i = 1; i < n; i++)
          if (greedy[i] / ratios[i] < greedy[basket] / ratios[basket]) basket = i;
        greedy[basket] += 2 ** level;
      });
      if (!breather && greedy.every((mass, i) => mass === target[i])) continue;
      const balls = [];
      initialLevels.forEach((levels, basketIndex) => {
        const width = levels.reduce((sum, level) => sum + 2 * radius(level), 0) + (levels.length - 1) * 0.12;
        let edge = -width / 2;
        levels.forEach(level => {
          const r = radius(level);
          balls.push({ ballId: balls.length + 1, level, basketIndex, positionSpace: 1,
            x: round(edge + r), y: round(-4.24 + r + 0.05), rotZ: 0, canMerge: true });
          edge += 2 * r + 0.12;
        });
      });
      const theme = themes[(stage - 31) % themes.length];
      selected = {
        stage, mapId, title: `${theme[0]} ${stage}`, learningGoal: theme[1],
        difficulty: Math.min(10, 4 + chapter - (breather ? 1 : 0)),
        patternId: `puzzle-${stage}`, levelTolerance: mapId === 1 ? 4 : mapId === 2 ? 3.5 : 3,
        holdTime: mapId === 1 ? 1.5 : 2, settleTime: 3, forcedMergeTargetBallId: -1,
        balls, playerBallQueue: queue,
        clearRequirements: { allProvidedBallsUsed: true, allBallsInBaskets: true, allBeamsLevel: true },
        referenceSolution: {
          steps: drops.map((drop, i) => ({ queueIndex: i, level: drop.level, basketIndex: drop.basket, dropX: 0 })),
          initialMassUnits: initial, finalMassUnits: target, canonicalFinalLevels: target.map(bits),
          maxPossibleMergedLevel: Math.floor(Math.log2(Math.max(...target))),
          purpose: '배분 검증용 참고 풀이. 투입 위치와 물리적 성공은 별도 검증이 필요하다.', physicsVerified: false,
        },
      };
      const signature = logicalSignature(selected);
      if (signatures.has(signature)) { selected = undefined; continue; }
      signatures.add(signature);
      break;
    }
    assert(selected, `Could not construct stage ${stage}`);
    stages.push(selected);
  }
}

module.exports = { expandCampaign, logicalSignature, allocationCount, radius };
