const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {logicalSignature, allocationCount, radius} = require('./expand-campaign.cjs');
const root = path.resolve(__dirname,'../..');
const db = JSON.parse(fs.readFileSync(path.join(root,'Assets/Aset/Data/StageData.json'),'utf8'));
const managerCode = fs.readFileSync(path.join(root,'Assets/Script/Manager/GameManager.cs'),'utf8');
const weights = managerCode.match(/return new List<float>\(\)\s*\{([^}]+)\}/s)[1].split(',').map(s=>Number(s.trim().replace(/f$/,'')));
const results = [];
const ids = new Set();
const signatures = new Set();
const logicalSignatures = new Map();
const baselineDuplicates = [];
const weight = level => weights[level];
assert.equal(weight(0),1);
assert(!JSON.stringify(db).includes('weightByLevel'),'Weights belong in GameManager');
for(let i=1;i<weights.length;i++)
  assert.equal(weight(i),2*weight(i-1),'Merge must conserve mass');

for(const s of db.stages) {
  assert(!ids.has(s.stage)); ids.add(s.stage);
  const map = db.maps.find(m=>m.mapId===s.mapId);
  assert(map,'Unknown map');
  const n = map.basketCount;
  assert.equal(map.targetWeightRatios.length,n);
  const initial = Array(n).fill(0);
  const ballIds = new Set();
  for(const ball of s.balls) {
    assert(!ballIds.has(ball.ballId)); ballIds.add(ball.ballId);
    assert(Number.isInteger(ball.level) && ball.level>=0 && ball.level<weights.length);
    assert(Number.isFinite(weight(ball.level)));
    assert(Number.isInteger(ball.basketIndex) && ball.basketIndex>=0 && ball.basketIndex<n);
    assert.equal(ball.positionSpace,1);
    assert(Number.isFinite(ball.x) && Number.isFinite(ball.y));
    assert.equal(typeof ball.canMerge,'boolean','Merge state must be explicit');
    const r=radius(ball.level);
    assert(Math.abs(ball.x)+r<=11.7,'Initial ball outside basket width');
    assert(ball.y-r>=-4.24 && ball.y+r<=32,'Initial ball outside basket height');
    initial[ball.basketIndex]+=weight(ball.level);
  }
  for(let i=0;i<s.balls.length;i++) for(let j=i+1;j<s.balls.length;j++) {
    const a=s.balls[i], b=s.balls[j];
    if(a.basketIndex===b.basketIndex)
      assert(Math.hypot(a.x-b.x,a.y-b.y)+0.001>=radius(a.level)+radius(b.level),'Initial balls overlap');
  }
  for(const level of s.playerBallQueue)
    assert(Number.isInteger(level) && level>=0 && level<weights.length);
  const total = initial.reduce((a,b)=>a+b,0)+s.playerBallQueue.reduce((a,l)=>a+weight(l),0);
  const ratioTotal=map.targetWeightRatios.reduce((a,b)=>a+b,0);
  const unit=total/ratioTotal;
  assert.equal(unit%weight(0),0,'Target must be representable');
  const target=map.targetWeightRatios.map(r=>r*unit);
  assert(initial.some((w,i)=>w!==target[i]),'Stage must start unbalanced');
  const final=[...initial];
  assert.equal(s.referenceSolution.steps.length,s.playerBallQueue.length);
  s.referenceSolution.steps.forEach((step,i)=>{
    assert.equal(step.queueIndex,i); assert.equal(step.level,s.playerBallQueue[i]);
    assert(step.basketIndex>=0 && step.basketIndex<n);
    final[step.basketIndex]+=weight(step.level);
  });
  assert.deepEqual(final,target,'Reference solution fails target ratio');
  assert.deepEqual(final.map(w=>w/weight(0)),s.referenceSolution.finalMassUnits);
  s.referenceSolution.canonicalFinalLevels.forEach((levels,i)=>{
    assert.equal(levels.reduce((sum,l)=>sum+weight(l),0),final[i]);
  });
  const signature=JSON.stringify([s.mapId,s.balls.map(b=>[b.level,b.basketIndex]),s.playerBallQueue]);
  assert(!signatures.has(signature),'Exact duplicate stage'); signatures.add(signature);
  const logical=logicalSignature(s);
  if(logicalSignatures.has(logical)) {
    assert(s.stage<=30,'New stage duplicates an existing allocation under symmetry/scaling');
    baselineDuplicates.push([logicalSignatures.get(logical),s.stage]);
  } else logicalSignatures.set(logical,s.stage);

  // 모든 투입 목적지를 열거한다. 같은 종류의 공 순서만 다른 풀이는 배분별로 묶는다.
  let sequenceCount=0;
  const patterns=new Set();
  const masses=[...initial];
  const assigned=Array.from({length:n},()=>Array(weights.length).fill(0));
  function search(index) {
    if(index===s.playerBallQueue.length) {
      if(masses.every((mass,i)=>mass===target[i])) {
        sequenceCount++;
        patterns.add(JSON.stringify(assigned));
      }
      return;
    }
    const level=s.playerBallQueue[index];
    const mass=weight(level);
    for(let b=0;b<n;b++) {
      if(masses[b]+mass>target[b]) continue;
      masses[b]+=mass; assigned[b][level]++;
      search(index+1);
      masses[b]-=mass; assigned[b][level]--;
    }
  }
  search(0);
  assert(sequenceCount>0,'No allocation exists');
  if(s.stage>1) {
    assert(s.playerBallQueue.length>=4 && s.playerBallQueue.length<=8,'Need 4-8 planned drops');
    assert(new Set(s.playerBallQueue).size>=2,'Need different animal types');
    assert(new Set(s.referenceSolution.steps.map(step=>step.basketIndex)).size>=2,'Need multi-basket allocation');
  }
  // 현재 가장 가벼운 바구니만 고르는 전략이 통하는지 별도 기록한다.
  const greedyMasses=[...initial];
  for(const level of s.playerBallQueue) {
    let basket=0;
    for(let b=1;b<n;b++)
      if(greedyMasses[b]/map.targetWeightRatios[b]<greedyMasses[basket]/map.targetWeightRatios[basket]) basket=b;
    greedyMasses[basket]+=weight(level);
  }
  assert.equal(s.clearRequirements.allProvidedBallsUsed,true);
  assert.equal(s.clearRequirements.allBallsInBaskets,true);
  assert.equal(patterns.size,allocationCount(initial,target,s.playerBallQueue),'Independent allocation counts disagree');
  results.push({stage:s.stage,title:s.title,mapId:s.mapId,providedBallCount:s.playerBallQueue.length,
    finalMassUnits:s.referenceSolution.finalMassUnits,validDropSequences:sequenceCount,
    distinctLevelAllocations:patterns.size,
    greedyLightestBasketClears:greedyMasses.every((mass,i)=>mass===target[i]),physicsVerified:false});
}
assert.deepEqual([...ids],Array.from({length:100},(_,i)=>i+1));
assert.equal(db.chapters[0].from,1);
assert.equal(db.chapters.at(-1).to,100);
for(let i=1;i<db.chapters.length;i++) assert.equal(db.chapters[i].from,db.chapters[i-1].to+1);
const report={stageCount:results.length,passed:true,
  baselineLogicalDuplicates:baselineDuplicates,
  verified:['Mass conservation','All finite queues have a solution','Target ratios of every beam',
    'Reference solution uses every provided ball','No exact duplicate puzzles','Canonical merge results',
    'New stages exclude symmetry and uniform scaling duplicates','Initial circle bounds and non-overlap'],
  notVerified:['Unity trajectories and collisions','Basket tilt and game-over risk',
    'Time to settle','Angle tolerance may permit unintended solutions','Actual merged ball occupancy'],
  stages:results};
fs.writeFileSync(path.join(__dirname,'Validation.json'),JSON.stringify(report,null,2)+'\n');
console.log(`PASS: ${results.length} puzzles. Logical reachability only; Unity physics QA pending.`);
