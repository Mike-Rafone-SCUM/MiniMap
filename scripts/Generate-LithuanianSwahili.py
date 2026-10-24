"""Offline catalogue authoring; only public English labels leave this machine.

Uses the existing protected-token translation workflow. Cached drafts and explicit
terminology corrections are retained. Native-speaker approval remains pending.
Never invoked by a build or by the application.
"""
import concurrent.futures
import importlib.util
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf8')

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('additional', ROOT/'scripts/Generate-AdditionalLocalizations.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
FOLDER = ROOT/'release/checks/baltic-swahili'
reference = json.loads((FOLDER/'reference.json').read_text(encoding='utf-8-sig'))
TABLES = [('Strings', list(reference['StringsEn'].items())),
          ('SectionNames', list(reference['SectionNamesPt'].items())),
          ('CategoryNames', list(reference['CategoryNamesPt'].items())),
          ('MapObjects', list(reference['MapObjectsEsAr'].items()))]
RESOURCES = base.RESOURCE_TABLES
labels = list(dict.fromkeys(value for _, rows in TABLES+RESOURCES for _, value in rows))
OPTICS = {
    'lt': ['Dėtuvė','Optinis taikiklis','Duslintuvas','Šovinių apkaba','Tvirtinimo bėgelis','Raudono taško taikiklis','Holografinis taikiklis','Refleksinis taikiklis'],
    'sw': ['Kasha la risasi','Darubini ya kulenga','Kipunguza sauti','Kishikio cha risasi','Reli ya kufungia','Kilengezi cha nukta nyekundu','Kilengezi cha hologramu','Kilengezi cha mwanga']
}
AMMO = {
    'lt': ['Šoviniai','Savadarbiai šoviniai','Šarvamušiai šoviniai','Savadarbiai šarvamušiai šoviniai','Trasavimo šoviniai','Šovinių dėžutė','Šarvamušių šovinių dėžutė','Trasavimo šovinių dėžutė'],
    'sw': ['Risasi','Risasi zilizotengenezwa kwa mikono','Risasi za kutoboa kinga','Risasi za kutoboa kinga zilizotengenezwa kwa mikono','Risasi za kuonyesha njia','Sanduku la risasi','Sanduku la risasi za kutoboa kinga','Sanduku la risasi za kuonyesha njia']
}
OPTIC_MODELS = {
    'lt': {'Improvised':'savadarbis','Improvised Bottle':'savadarbis iš butelio','Improvised Can':'savadarbis iš skardinės','Improvised Oil Can':'savadarbis iš alyvos skardinės','Improvised Rifle':'savadarbiam šautuvui','Long Improvised':'ilgas savadarbis','Short Improvised':'trumpas savadarbis','Handgun':'pistoletui','Hunting':'medžioklinis','12 Gauge':'12 kalibro','Cal .30-06':'kalibras .30-06'},
    'sw': {'Improvised':'iliyotengenezwa kwa mikono','Improvised Bottle':'iliyotengenezwa kwa chupa','Improvised Can':'iliyotengenezwa kwa kopo','Improvised Oil Can':'iliyotengenezwa kwa kopo la mafuta','Improvised Rifle':'ya bunduki iliyotengenezwa kwa mikono','Long Improvised':'ndefu iliyotengenezwa kwa mikono','Short Improvised':'fupi iliyotengenezwa kwa mikono','Handgun':'ya bastola','Hunting':'ya uwindaji','12 Gauge':'kaliba 12','Cal .30-06':'kaliba .30-06'}
}

def item_label(english, translated, code, memory):
    suffixes={'Magazine':0,'Scope':1,'Suppressor':2,'Clip':3,'Rail':4,'Red Dot Sight':5,'Red Dot':5,'Holographic Sight':6,'Reflex Sight':7}
    for suffix in sorted(suffixes,key=len,reverse=True):
        if english.endswith(' '+suffix):
            model=english[:-len(suffix)-1]
            return OPTICS[code][suffixes[suffix]]+' — '+OPTIC_MODELS[code].get(model,memory.get(model,model))
    ammo=re.match(r'^([.0-9]\S*(?: (?:ACP|AE|BMG))?) (.+)$',english)
    if ammo and any(term in ammo[2] for term in ['Ammo','Crafted','Armor Piercing Bullet']):
        calibre,kind=ammo.groups()
        ap='AP' in kind or 'Armor Piercing' in kind
        tracer='TR' in kind or 'Tracer' in kind
        crafted='Crafted' in kind
        index=(6 if ap else 7 if tracer else 5) if 'Box' in kind else (3 if crafted else 2) if ap else 4 if tracer else 1 if crafted else 0
        return AMMO[code][index]+' — '+calibre+(' (AP)' if 'AP' in kind else '')+(' (TR)' if 'TR' in kind else '')
    return translated

def generate(code, suffix):
    cache = FOLDER/(code+'.json')
    memory = json.loads(cache.read_text(encoding='utf8')) if cache.exists() else {}
    reviewed = json.loads((ROOT/'resources/lithuanian-swahili-review.json').read_text(encoding='utf8'))
    overrides = reviewed[code]
    memory.update(overrides)
    for label in labels:
        if not re.search(r'[A-Za-z]{2,}', base.author.PROTECT.sub('',label)):
            memory[label] = label
    missing = [label for label in labels if label not in memory]
    while missing:
        batch = []
        while missing and len(batch)<30 and sum(map(len,batch))+len(missing[0])<1500:
            batch.append(missing.pop(0))
        if not batch: batch.append(missing.pop(0))
        memory.update(zip(batch,base.translate_checked(batch,code)))
        cache.write_text(json.dumps(memory,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
        print(code,len(memory),'/',len(labels),flush=True)
    memory.update(overrides)
    cache.write_text(json.dumps(memory,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    output = ''
    for prefix, rows in TABLES:
        output += '        '+('public' if prefix=='Strings' else 'private')+' static readonly Dictionary<string,string> '+prefix+suffix+' = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {\n'
        output += ''.join('            { '+json.dumps(key,ensure_ascii=False)+', '+json.dumps(memory[value],ensure_ascii=False)+' },\n' for key,value in rows)
        output += '        };\n'
    for prefix, rows in RESOURCES:
        values = [(key,item_label(value,memory[value],code,memory) if prefix=='spawn-item-names' else memory[value]) for key,value in rows]
        if prefix=='spawn-category-names':values=[(key,reviewed['categories'][code][key]) for key,_ in values]
        for key,value in values:
            if not value.strip() or '\n' in value or '\t' in value or len(value)>256:
                raise ValueError('Invalid catalogue label: '+code+' / '+key)
        (ROOT/'resources'/(prefix+'-'+code+'.tsv')).write_text('# Offline localized catalogue; native-speaker review pending.\n'+''.join(key+'\t'+value+'\n' for key,value in values),encoding='utf8')
    return output

if __name__=='__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        results=list(pool.map(lambda pair:generate(*pair),[('lt','Lt'),('sw','Sw')]))
    (ROOT/'src/Localization.LithuanianSwahili.cs').write_text('// Offline localizations; native-speaker review pending.\nusing System;\nusing System.Collections.Generic;\nnamespace ScumMiniMap {\n    public static partial class Localization {\n'+''.join(results)+'    }\n}\n',encoding='utf8')
