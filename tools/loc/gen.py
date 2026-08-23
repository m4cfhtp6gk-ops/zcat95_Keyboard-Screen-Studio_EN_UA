# -*- coding: utf-8 -*-
"""Emit the three embedded string catalogues from one side-by-side table."""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from strings_core import CORE
from strings_screen import SCREEN
from strings_ui import UI

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(REPO_ROOT, 'src', 'KeyboardScreen.Core', 'Localization')
ENTRIES = CORE + SCREEN + UI

keys = [k for k, *_ in ENTRIES]
dups = sorted({k for k in keys if keys.count(k) > 1})
if dups:
    sys.exit('duplicate keys: %s' % dups)

def norm_uk(text):
    # One apostrophe form everywhere in Ukrainian (пам'ять, прев'ю).
    return text.replace('ʼ', "'").replace('’', "'")

for lang, index in (('en', 1), ('uk', 2), ('zh-Hans', 3)):
    table = {}
    for entry in ENTRIES:
        value = entry[index]
        table[entry[0]] = norm_uk(value) if lang == 'uk' else value
    path = os.path.join(OUT, 'Strings.%s.json' % lang)
    with open(path, 'w', encoding='utf-8') as handle:
        json.dump(table, handle, ensure_ascii=False, indent=2, sort_keys=False)
        handle.write('\n')
    print('%-8s %4d keys -> %s' % (lang, len(table), path))

placeholders = {}
for entry in ENTRIES:
    for index, lang in ((1, 'en'), (2, 'uk'), (3, 'zh-Hans')):
        marks = sorted(set(__import__('re').findall(r'\{(\d+)\}', entry[index])))
        placeholders.setdefault(entry[0], {})[lang] = marks
mismatch = [k for k, v in placeholders.items() if len({tuple(m) for m in v.values()}) > 1]
if mismatch:
    sys.exit('placeholder mismatch between languages: %s' % mismatch)
print('placeholders consistent across languages')
