import json, re, pathlib
root = pathlib.Path(__file__).resolve().parents[2]
out = root / 'outputs/authoring-20261002'
snapshot = json.loads((out / 'snapshot.json').read_text(encoding='utf-8'))
entries = snapshot['texts'] + json.loads((out / 'code-texts.json').read_text(encoding='utf-8'))
# Whole logical lines keep independent effects separate. Numeric slots generalize
# level variants without sending strings to an external translation service.
pattern = re.compile(r'<[^>]+>|\{[^{}]+\}|\d+(?:\.\d+)?')
def template(text):
    values=[]
    def sub(m):
        if m[0].startswith(('<','{')): return m[0]
        values.append(m[0]); return '⟦' + str(len(values)-1) + '⟧'
    return pattern.sub(sub,text),values
templates=[]
for entry in entries:
    for line in entry['ko'].split('\n'):
        text,values=template(line)
        if re.search('[가-힣]',text) and text not in templates: templates.append(text)
(out/'translation-templates.json').write_text(json.dumps(templates,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'all-texts.json').write_text(json.dumps(entries,ensure_ascii=False,indent=2),encoding='utf-8')
print('Text sites:',len(entries),'Translation templates:',len(templates))
for i,t in enumerate(templates[:160]): print(str(i)+'\t'+t)
