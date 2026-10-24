"""Author optional synthesized navigation clips; playback is entirely local.

Requires edge-tts in the authoring environment. Never run by builds or the app.
"""
import argparse
import asyncio
import json
import sys
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--dependency-path')
args=parser.parse_args()
if args.dependency_path:sys.path.insert(0,args.dependency_path)
import edge_tts
ROOT=Path(__file__).resolve().parents[1]
CLIPS=['01_500_meters.mp3','02_250_meters.mp3','03_100_meters.mp3','04_50_meters.mp3','05_continue_straight.mp3','06_turn_left.mp3','07_turn_right.mp3','08_keep_left.mp3','09_keep_right.mp3','10_make_a_u_turn.mp3','12_you_have_arrived.mp3','13_recalculating.mp3']
PACKS=[
    ('Ona (Lietuvių)','lt-LT-OnaNeural',[
        'Už penkių šimtų metrų.','Už dviejų šimtų penkiasdešimties metrų.','Už šimto metrų.','Už penkiasdešimties metrų.',
        'Važiuokite tiesiai.','Sukite į kairę.','Sukite į dešinę.','Laikykitės kairės.','Laikykitės dešinės.','Apsisukite.','Atvykote į tikslą.','Perskaičiuojamas maršrutas.']),
    ('Zuri (Kiswahili)','sw-KE-ZuriNeural',[
        'Baada ya mita mia tano.','Baada ya mita mia mbili na hamsini.','Baada ya mita mia moja.','Baada ya mita hamsini.',
        'Endelea moja kwa moja.','Geuka kushoto.','Geuka kulia.','Fuata upande wa kushoto.','Fuata upande wa kulia.','Geuka urudi nyuma.','Umefika.','Inakokotoa njia upya.'])
]
async def main():
    voices={entry['ShortName'] for entry in await edge_tts.list_voices()}
    transcript=[]
    for name,voice,texts in PACKS:
        assert voice in voices,voice
        folder=ROOT/'resources/voice-navigation'/name
        folder.mkdir(exist_ok=True)
        for file,text in zip(CLIPS,texts):
            output=folder/file
            if not output.exists():await edge_tts.Communicate(text,voice,rate='-5%').save(str(output))
            assert output.stat().st_size>1000,file
            print(voice,file,output.stat().st_size,flush=True)
        transcript.append(dict(name=name,voice=voice,clips=dict(zip(CLIPS,texts)),nativeSpeakerApproval='pending'))
    (ROOT/'release/checks/baltic-swahili/voice-transcripts.json').write_text(json.dumps(transcript,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
asyncio.run(main())
