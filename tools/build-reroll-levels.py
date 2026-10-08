#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 Cyanmir
# 从普通效果模板的 Traits 引用解析数字等级，只导出最小等级索引及来源散列。
# 不从 I/II/III 名称猜等级，不收录 TierUnique，也不把定义上限当作当前装备实际可刷的最高要求。
import argparse
import hashlib
import json
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description="Build numeric effect level bounds from a local game table")
    parser.add_argument("table", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--game-build", required=True)
    args = parser.parse_args()
    raw = args.table.read_bytes()
    rows = json.loads(raw)
    by_tag = {row['TypeTag']['TagName']: row for row in rows}
    def tiers(tag, trail):
        if tag == 'SW.EffectTemplate.TierUnique': return set()
        if tag.startswith('SW.Effect.Tier.'):
            text = tag[len('SW.Effect.Tier.'):]
            return {int(text)} if text.isdigit() and 1 <= int(text) <= 1000 else set()
        if tag not in by_tag: return set()
        if tag in trail: raise ValueError('Cyclic template trait')
        result = set()
        for trait in by_tag[tag].get('Traits', {}).get('GameplayTags', []):
            result.update(tiers(trait['TagName'], trail | {tag}))
        return result
    levels = {}
    for row in rows:
        effect = row.get('TemplateData', {}).get('Effect', {}).get('TagName', '')
        if not effect.startswith('SW.Effect.'): continue
        traits = [value['TagName'] for value in row.get('Traits', {}).get('GameplayTags', [])]
        if 'SW.EffectTemplate.TierUnique' in traits: continue
        values = tiers(row['TypeTag']['TagName'], set())
        if len(values) == 1: levels.setdefault(effect, set()).update(values)
    output = dict(format=1, gameBuild=args.game_build, source=args.table.name,
        sourceSha256=hashlib.sha256(raw).hexdigest(),
        scope='Numeric tiers defined by ordinary effect templates; excludes Unique',
        levels={key: sorted(value) for key, value in sorted(levels.items())})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, indent=2) + '\n', encoding='utf-8')
    print(f'Wrote numeric level bounds for {len(levels)} effects')

if __name__ == '__main__': main()
