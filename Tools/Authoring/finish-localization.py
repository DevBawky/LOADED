import json,re,pathlib,collections
root=pathlib.Path(__file__).resolve().parents[2]
out=root/'outputs/authoring-20261002'
translations=json.loads((root/'Tools/Authoring/translations.json').read_text(encoding='utf-8'))
pattern=re.compile(r'<[^>]+>|\{[^{}]+\}|\d+(?:\.\d+)?')
def translate(line):
    vals=[]
    def sub(m):
        if m[0].startswith(('<','{')): return m[0]
        vals.append(m[0]); return '⟦'+str(len(vals)-1)+'⟧'
    template=pattern.sub(sub,line)
    if not re.search('[가-힣]',template): return line
    if template not in translations: raise ValueError('Missing translation: '+template)
    value=translations[template]
    assert sorted(re.findall(r'⟦\d+⟧',value))==sorted(re.findall(r'⟦\d+⟧',template)), (template,value)
    return re.sub(r'⟦(\d+)⟧',lambda m:vals[int(m[1])],value)
entries=json.loads((out/'all-texts.json').read_text(encoding='utf-8'))
excluded=[]; result=[]
for e in entries:
    if e['scope']=='Code' and (e['ko'].startswith(('\\d+','(?<poison>')) or e['ko'] in ['강화|제거|무료|비용|골드|탄환|아이템','니다. ','니다.']):
        e['reason']='Text-processing pattern, not displayed UI text'; excluded.append(e); continue
    e['en']='\n'.join(translate(line) for line in e['ko'].split('\n'))
    e['status']='Review'
    if '/Editor/' in e['source']: e['scope']='Editor'
    if e['scope']=='Code': e['note']='Runtime binding required. '+e['note']
    elif e['scope']=='Editor': e['note']='Editor-only label. '+e['note']
    else: e['note']='Serialized source; String Table binding required. '+e['note']
    for token_pattern in [r'(?<!\{)\{[^{}]+\}(?!\})',r'</?[A-Za-z][^>]*>']:
        assert sorted(re.findall(token_pattern,e['ko']))==sorted(re.findall(token_pattern,e['en'])),e
    assert e['ko'].count('\n')==e['en'].count('\n')
    assert not re.search('[가-힣]',e['en']), e
    result.append(e)
assert len({e['key'] for e in result})==len(result)
(root/'Tools/Authoring/localization.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'Tools/Authoring/localization-excluded.json').write_text(json.dumps(excluded,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'entries':len(result),'excluded':len(excluded),'scopes':dict(collections.Counter(e['scope'] for e in result))},ensure_ascii=False))
