import fs from 'node:fs/promises';
import path from 'node:path';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';

const root=path.resolve(import.meta.dirname,'../..');
const output=path.join(root,'outputs/authoring-20261002');
const snapshot=JSON.parse(await fs.readFile(path.join(output,'snapshot.json'),'utf8'));
const icons=new Map(snapshot.bullets.map(b=>[b.guid,b.icon]));
const jobs=[['bullet-native.xlsx','LOADED_BulletData.xlsx'],['localization-native.xlsx','LOADED_Localization_ko_en.xlsx']];
if(process.argv.includes('--bullets')) jobs.splice(1);
const targetIndex=process.argv.indexOf('--target');
const targetOverride=targetIndex>=0?path.resolve(process.argv[targetIndex+1]):null;
const col=n=>{let s='';for(n++;n>0;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;};
for(const [source,target] of jobs){
  const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path.join(output,source)));
  if(source.startsWith('localization')){
    // Baselines come from the saved Unity tables, verified against every source row.
    const current=JSON.parse(await fs.readFile(path.join(output,'localization-current.json'),'utf8'));
    const meta=wb.worksheets.getItem('메타데이터');
    meta.getRange(`A2:B${current.length+2}`).values=[['schema','LOADED.Localization.1'],...current.map(([key,ko,en,state])=>[key,JSON.stringify([ko,en,state])])];
  }
  const summary=await wb.inspect({kind:'sheet',include:'id,name'});
  const sheets=[];
  for(let i=0;;i++){try{const s=wb.worksheets.getItemAt(i);if(!s)break;sheets.push(s);}catch{break;}}
  for(const sheet of sheets){
    const values=sheet.getUsedRange().values, rows=values.length, columns=values[0].length;
    if(source.startsWith('bullet') && (sheet.name.endsWith('형') || sheet.name==='아이콘')){
      const isType=sheet.name.endsWith('형');
      sheet.deleteAllDrawings();
      const grid=sheet.getRange(`A1:${col(columns-1)}${rows}`);
      grid.format={font:{name:'Malgun Gothic',size:11,color:'#243247'},rowHeight:32,verticalAlignment:'center',wrapText:true,columnWidth:13};
      sheet.showGridLines=false; sheet.freezePanes.freezeRows(1);
      sheet.tabColor=isType?'#587696':'#C68A27';
      sheet.getRange(`A1:${col(columns-1)}1`).format={fill:'#28364B',font:{name:'Malgun Gothic',size:11,bold:true,color:'#FFFFFF'},rowHeight:44};
      sheet.getRange(`A1:A${rows}`).format.columnWidth=11;
      sheet.getRange(`B1:B${rows}`).format.columnWidth=20;
      sheet.getRange(`C1:C${rows}`).format.columnWidth=13;
      if(isType){
        sheet.getRange(`D1:F${rows}`).format.columnWidth=9;
        sheet.getRange(`G1:H${rows}`).format.columnWidth=13;
        sheet.getRange(`K1:K${rows}`).format.columnWidth=9;
        sheet.getRange(`N1:N${rows}`).format.columnWidth=60;
        sheet.getRange(`O1:O${rows}`).format.columnWidth=38;
      } else sheet.getRange(`E1:E${rows}`).format.columnWidth=38;
      const gradeColors={Normal:'#E8EDF2',Rare:'#DCEAF8',Ace:'#EADFF5',Legendary:'#F7EAC5'};
      for(let r=1;r<rows;r++){
        const row=values[r], guid=row[isType?14:4];
        const line=sheet.getRange(`A${r+1}:${col(columns-1)}${r+1}`);
        if(!guid){
          const grade=String(row[1]??'').split(' ')[0];
          line.format={fill:gradeColors[grade]??'#E8EDF2',font:{bold:true},rowHeight:29};
          continue;
        }
        const basic=!isType || Number(row[3])===0;
        line.format.fill='#F4F6F8';
        if(isType){
          for(const c of [3,4,5,6,7,8,10,11,...(basic?[12]:[])]) if(row[c]!=='' && row[c]!=null) sheet.getCell(r,c).values=[[Number(row[c])]];
          sheet.getRange(`E${r+1}:L${r+1}`).format.fill='#FFF8DF';
          sheet.getRange(`N${r+1}`).format.fill='#FFF8DF';
          const lines=String(row[13]??'').split('\n').reduce((n,l)=>n+Math.max(1,Math.ceil(l.length/36)),0);
          line.format.rowHeight=Math.max(basic?58:34,lines*16+10);
          sheet.getRange(`D${r+1}`).format.numberFormat='"+"0;"-"0;"기본"';
          sheet.getRange(`E${r+1}:M${r+1}`).format.numberFormat='0';
          sheet.getRange(`G${r+1}`).format.numberFormat='0.0';
          sheet.getRange(`H${r+1}`).format.numberFormat='0.00';
          sheet.getRange(`K${r+1}`).format.numberFormat='0.00';
          sheet.getRange(`J${r+1}`).dataValidation={rule:{type:'list',values:['true','false']}};
          if(basic){
            sheet.getRange(`B${r+1}:C${r+1}`).format.fill='#FFF8DF';
            sheet.getRange(`M${r+1}`).format.fill='#FFF8DF';
            sheet.getRange(`B${r+1}`).format.font.bold=true;
            sheet.getRange(`C${r+1}`).dataValidation={rule:{type:'list',values:['Normal','Rare','Ace','Legendary']}};
            line.format.borders={top:{style:'thin',color:'#B8C5D3'}};
          }
        }else line.format.rowHeight=58;
        if(basic){
          const bytes=await fs.readFile(icons.get(guid));
          sheet.images.add({dataUrl:'data:image/png;base64,'+bytes.toString('base64'),anchor:{from:{row:r,col:0,rowOffsetPx:6,colOffsetPx:6},extent:{widthPx:56,heightPx:56}}});
        }
      }
      const ranges=isType?['A1:H7','I1:N7']:['A1:D5'];
      for(let i=0;i<ranges.length;i++){
        const preview=await wb.render({sheetName:sheet.name,range:ranges[i],scale:1.3,format:'png'});
        await fs.writeFile(path.join(output,`Bullet_v2_${sheet.name}_${i}.png`),new Uint8Array(await preview.arrayBuffer()));
      }
      continue;
    }
    const range=sheet.getRange(`A1:${col(columns-1)}${rows}`);
    range.format.font={name:'Malgun Gothic',size:11,color:'#243247'};
    range.format.wrapText=true; range.format.verticalAlignment='top';range.format.columnWidth=19;
    range.format.rowHeight=32;
    sheet.showGridLines=false;
    sheet.getRange(`A1:${col(columns-1)}1`).format={fill:'#28364B',font:{name:'Malgun Gothic',size:11,bold:true,color:'#FFFFFF'},rowHeight:36,wrapText:true};
    if(rows>1)sheet.getRange(`A2:${col(columns-1)}${rows}`).format.fill='#FFF8DF';
    sheet.freezePanes.freezeRows(1);
    if(sheet.name==='사용 안내'){
      sheet.getRange(`A1:A${rows}`).format.columnWidth=23;sheet.getRange(`B1:B${rows}`).format.columnWidth=115;
      sheet.getRange(`A2:B${rows}`).format.fill='#F0F3F7';sheet.getRange(`A2:B${rows}`).format.rowHeight=62;
    }else if(sheet.name==='메타데이터'){
      range.format.fill='#EDF0F4'; sheet.getRange(`A1:B1`).format.fill='#28364B';
      sheet.getRange(`A1:A${rows}`).format.columnWidth=50;sheet.getRange(`B1:B${rows}`).format.columnWidth=80;
      sheet.getRange(`A2:B${rows}`).format.wrapText=false;
    }else{
      sheet.tables.add(`A1:${col(columns-1)}${rows}`,true,`Data_${source.startsWith('bullet')?'B':'L'}_${sheets.indexOf(sheet)}`);
      if(sheet.name==='탄환'){
        sheet.deleteAllDrawings();
        for(let r=1;r<rows;r++){
          const bytes=await fs.readFile(icons.get(values[r][5]));
          sheet.images.add({dataUrl:'data:image/png;base64,'+bytes.toString('base64'),anchor:{from:{row:r,col:0,rowOffsetPx:8,colOffsetPx:8},extent:{widthPx:64,heightPx:64}}});
        }
        sheet.getRange(`A1:A${rows}`).format.columnWidth=13;sheet.getRange(`B1:B${rows}`).format.columnWidth=24;
        sheet.getRange(`A2:F${rows}`).format.rowHeight=66;
        sheet.getRange(`F2:F${rows}`).format={fill:'#EDF0F4',columnWidth:38};
        sheet.getRange(`C2:C${rows}`).dataValidation={rule:{type:'list',values:['Normal','Rare','Ace','Legendary']}};
        sheet.getRange(`D2:D${rows}`).dataValidation={rule:{type:'list',values:['Normal','Ghost','Sniper','Storm','Shotgun','Piercing','Debuff','Kinetic','Combo','Economy','Growth','Blood']}};
      }else if(sheet.name==='레벨'){
        sheet.getRange(`A1:A${rows}`).format.columnWidth=22; sheet.getRange(`C1:C${rows}`).format.columnWidth=65;
        sheet.getRange(`A2:B${rows}`).format.fill='#EDF0F4'; sheet.getRange(`L2:L${rows}`).format={fill:'#EDF0F4',columnWidth:38};
        sheet.getRange(`I2:I${rows}`).dataValidation={rule:{type:'list',values:['true','false']}};
        for(let r=1;r<rows;r++)sheet.getRange(`A${r+1}:L${r+1}`).format.rowHeight=Math.max(55,21*(String(values[r][2]??'').split('\n').reduce((n,l)=>n+Math.max(1,Math.ceil(l.length/37)),0))+10);
      }else if(sheet.name==='현지화'){
        sheet.getRange(`A1:A${rows}`).format.columnWidth=23;
        sheet.getRange(`B1:C${rows}`).format.columnWidth=65;
        sheet.getRange(`D1:D${rows}`).format.columnWidth=15;
        sheet.getRange(`E1:F${rows}`).format.columnWidth=58;
        sheet.getRange(`G1:H${rows}`).format.columnWidth=46;
        sheet.getRange(`A2:A${rows}`).format.fill='#EDF0F4'; sheet.getRange(`E2:H${rows}`).format.fill='#EDF0F4';
        sheet.getRange(`D2:D${rows}`).dataValidation={rule:{type:'list',values:['Draft','Review','Approved']}};
        for(let r=1;r<rows;r++){
          const lineCount=(text,width)=>String(text??'').split('\n').reduce((n,l)=>n+Math.max(1,Math.ceil([...l].reduce((w,c)=>w+(/[가-힣]/.test(c)?1.65:1),0)/width)),0);
          const h=Math.max(72,21*Math.max(lineCount(values[r][1],65),lineCount(values[r][2],65),lineCount(values[r][4],58),lineCount(values[r][5],58),lineCount(values[r][7],46))+10);
          sheet.getRange(`A${r+1}:H${r+1}`).format.rowHeight=h;
        }
      }else{
        sheet.getRange(`A1:A${rows}`).format.columnWidth=24;
        if(sheet.name!=='초월 준비'){sheet.getRange(`${col(columns-1)}2:${col(columns-1)}${rows}`).format={fill:'#EDF0F4',columnWidth:38};}
        else sheet.getRange(`D1:D${rows}`).format.columnWidth=45;
      }
    }
    const end=sheet.name==='사용 안내'?'B7':sheet.name==='현지화'?'D5':sheet.name==='메타데이터'?'B4':`${col(Math.min(columns,6)-1)}5`;
    try{const preview=await wb.render({sheetName:sheet.name,range:`A1:${end}`,scale:1.4,format:'png'}); await fs.writeFile(path.join(output,`${target.replace('.xlsx','')}_${sheet.name}.png`),new Uint8Array(await preview.arrayBuffer()));}
    catch(error){console.log('Render failed',sheet.name,String(error));throw error;}
  }
  console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:10},maxChars:1500})).ndjson);
  console.log((await wb.inspect({kind:'table',range:source.startsWith('bullet')?'표준형!A1:H6':'현지화!A1:D3',tableMaxRows:6,tableMaxCols:8,maxChars:1500})).ndjson);
  const destination=targetOverride&&source.startsWith('bullet')?targetOverride:path.join(output,target);
  await fs.mkdir(path.dirname(destination),{recursive:true});
  await (await SpreadsheetFile.exportXlsx(wb)).save(destination);
  console.log('Saved',destination,sheets.length,'sheets');
}
