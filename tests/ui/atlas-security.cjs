const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const ts=require('../../front/node_modules/typescript');
const source=fs.readFileSync(path.join(__dirname,'../../front/src/components/financialChartData.ts'),'utf8');
const compiled=ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
const sandbox={exports:{}};vm.runInNewContext(compiled,sandbox);
const {parseFinancialChart,allowedMermaid}=sandbox.exports;
const valid={title:'Aging BRL',series:['saldo_centavos'],rows:[{label:'31-60',values:{saldo_centavos:12345}}]};
assert.equal(parseFinancialChart(JSON.stringify(valid)).rows[0].saldo_centavos,12345);
for(const invalid of [
  {...valid,series:['__proto__']}, {...valid,series:['label']},
  {...valid,rows:[{label:'x',values:{saldo_centavos:'123'}}]},
  {...valid,rows:[{label:'x',values:{saldo_centavos:null}}]},
  {...valid,rows:[{label:'x',values:{saldo_centavos:1e20}}]},
  {...valid,rows:Array(101).fill(valid.rows[0])},
  {...valid,series:['saldo_centavos','saldo_centavos']},
]) assert.equal(parseFinancialChart(JSON.stringify(invalid)),null);
assert.equal(parseFinancialChart('x'.repeat(50001)),null);
for(const code of ['flowchart TD\nA-->B','sequenceDiagram\nA->>B: Test','pie\n"A": 1','xychart-beta\nx-axis [A]\nbar [1]']) assert.equal(allowedMermaid(code),true);
for(const code of ['not mermaid','---\nconfig: unsafe','flowchart TD\n%%{init: {securityLevel: "loose"}}%%\nA-->B','flowchart TD\nclick A "javascript:alert(1)"','flowchart TD\nA[<script>alert(1)</script>]','flowchart TD\n'+'A'.repeat(20001)]) assert.equal(allowedMermaid(code),false);
console.log('PASS: chart shape/types/finite bounds/prototype keys/row limits, Mermaid type/size/config/callback/script rejection. Browser sandbox behavior tested separately.');
