import pathlib, json, re, hashlib, zipfile, xml.etree.ElementTree as ET
import yaml
root=pathlib.Path(__file__).resolve().parents[2]
out=root/'outputs/authoring-20261002'
def read_asset(path,strings=False):
    text=path.read_text(encoding='utf-8-sig')
    text=re.sub(r'^%.*\n','',text,flags=re.M)
    text=re.sub(r'^--- !u!\d+ &\d+.*$', '---', text, flags=re.M)
    return yaml.load(text,Loader=yaml.BaseLoader if strings else yaml.SafeLoader)['MonoBehaviour']
shared=read_asset(root/'Assets/Localization/Tables/LOADED Shared Data.asset',True)
keys={r['m_Id']:r['m_Key'] for r in shared['m_Entries']}
locale={code:{keys[r['m_Id']]:r['m_Localized'] for r in read_asset(root/f'Assets/Localization/Tables/LOADED_{code}.asset',True)['m_TableData']} for code in ['ko','en']}
sources=json.loads((root/'Tools/Authoring/localization.json').read_text(encoding='utf-8'))
assert len(keys)==len(sources)==len(locale['ko'])==len(locale['en'])
for row in sources:
    for code in ['ko','en']:
        assert locale[code][row['key']]==row[code],row['key']
        assert row[code].strip(),row['key']
(out/'localization-current.json').write_text(json.dumps([[key,locale['ko'][key],locale['en'][key],'Review'] for key in keys.values()],ensure_ascii=False),encoding='utf-8')
inventory=json.loads((out/'local-assets-inventory.json').read_text())
changed=[r['path'] for r in inventory if hashlib.sha256((root/'Assets/Package'/r['path']).read_bytes()).hexdigest()!=r['hash']]
assert not changed,changed
snapshot=json.loads((out/'snapshot.json').read_text(encoding='utf-8'))
def matches(expected,actual):
    if isinstance(expected,dict): return all(k in actual and matches(v,actual[k]) for k,v in expected.items())
    if isinstance(expected,list): return len(expected)==len(actual) and all(matches(a,b) for a,b in zip(expected,actual))
    if isinstance(expected,(float,int)) and isinstance(actual,(float,int)): return abs(expected-actual)<=1e-6*max(1,abs(expected))
    return expected==actual
for bullet in snapshot['bullets']:
    expected=json.loads(bullet['json'])['MonoBehaviour']; actual=read_asset(root/bullet['path'])
    assert matches(expected,actual),bullet['path']
report={'localizationKeys':len(keys),'localeEntriesVerified':sum(map(len,locale.values())),'missingTranslations':0,'preservedLocalAssetFiles':len(inventory),'unchangedBulletAssets':len(snapshot['bullets'])}
(out/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
