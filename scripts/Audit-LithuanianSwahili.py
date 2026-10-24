"""Validate the complete new checked-in catalogues without network access."""
import importlib.util
import json
import re
import unicodedata
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('author',ROOT/'scripts/Generate-LocalizationDrafts.py')
author=importlib.util.module_from_spec(spec);spec.loader.exec_module(author)
folder=ROOT/'release/checks/baltic-swahili'
reference=json.loads((folder/'reference.json').read_text(encoding='utf-8-sig'))
source=(ROOT/'src/Localization.LithuanianSwahili.cs').read_text(encoding='utf8')
items={row[0]:row[1] for row in author.rows(ROOT/'resources/spawn-items.tsv')}
categories=(ROOT/'src/Localization.SpawnItems.cs').read_text(encoding='utf8').split('PolishSpawnCategories = ReadPolishNames(@"',1)[1].split('");',1)[0]
category_keys={line.split('|')[0] for line in categories.splitlines()}
report={}
def validate(pairs,expected,label):
    actual=dict(pairs)
    assert len(pairs)==len(actual) and set(actual)==set(expected),label+': key coverage'
    for key,text in pairs:
        assert text.strip() and '\ufffd' not in text and 'ZXQ' not in text,label+': invalid text '+key
        assert not any('CYRILLIC' in unicodedata.name(c,'') for c in text),label+': unexpected Cyrillic '+key
    return actual
for code,suffix in [('lt','Lt'),('sw','Sw')]:
    counts={}
    for prefix,ref in [('Strings','StringsEn'),('SectionNames','SectionNamesPt'),('CategoryNames','CategoryNamesPt'),('MapObjects','MapObjectsEsAr')]:
        table=validate(author.dictionary(source,prefix+suffix),reference[ref],code+' '+prefix)
        counts[prefix]=len(table)
        if prefix=='Strings':
            for key,text in table.items():
                assert sorted(re.findall(r'\{\d+[^}]*\}',text))==sorted(re.findall(r'\{\d+[^}]*\}',reference[ref][key])),code+': format arguments '+key
                if 'Num /' in reference[ref][key]:assert 'Num /' in text,code+': physical key label '+key
    for prefix,expected in [('spawn-item-names',items),('spawn-category-names',category_keys),('map-object-names',reference['MapObjectsEsAr'])]:
        pairs=author.rows(ROOT/'resources'/(prefix+'-'+code+'.tsv'))
        actual=validate(pairs,expected,code+' '+prefix)
        assert all(len(value)<=256 and not any(unicodedata.category(c)=='Cc' for c in value) for value in actual.values()),code+': catalogue size/control characters'
        counts[prefix]=len(actual)
    report[code]=dict(coverage=counts,nativeSpeakerApproval='pending')
(folder/'catalogue-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps(report,ensure_ascii=False))
