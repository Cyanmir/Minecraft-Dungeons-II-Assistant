#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 Cyanmir
# 由对应版本模板直接 Traits 导出最小浏览分类；不解析/推断完整刷新池。
# 只输出标签、分类索引及来源散列，不拷贝原始数据表、词条范围或私人解包内容。
import argparse
import hashlib
import json
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description="Build effect browsing categories from a local game table")
    parser.add_argument("table", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--game-build", required=True)
    args = parser.parse_args()
    raw = args.table.read_bytes()
    categories = {}
    for row in json.loads(raw):
        effect = row.get("TemplateData", {}).get("Effect", {}).get("TagName", "")
        if not effect.startswith("SW.Effect."):
            continue
        traits = {value["TagName"] for value in row.get("Traits", {}).get("GameplayTags", [])}
        group = categories.setdefault(effect, set())
        if traits & {"SW.EffectTemplate.Weapon", "SW.EffectTemplate.MeleeWeapon"}:
            group.add(1)
        if traits & {"SW.EffectTemplate.Weapon", "SW.EffectTemplate.RangedWeapon", "SW.EffectTemplate.RangedWeaponQuiverArtifact", "SW.EffectTemplate.CrossbowOnly", "SW.EffectTemplate.BowQuiverOnly"}:
            group.add(2)
        if "SW.EffectTemplate.Armor" in traits:
            group.add(3)
        if traits & {"SW.EffectTemplate.Artifact", "SW.EffectTemplate.RangedWeaponQuiverArtifact", "SW.EffectTemplate.QuiverEffectTemplate"}:
            group.add(4)
    output = dict(format=1, gameBuild=args.game_build, source=args.table.name,
                  sourceSha256=hashlib.sha256(raw).hexdigest(),
                  scope="Effect template traits grouped for display",
                  categories={key: sorted(value or {5}) for key, value in sorted(categories.items())})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(categories)} display classifications")

if __name__ == "__main__":
    main()
