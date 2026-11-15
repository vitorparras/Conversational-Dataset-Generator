import csv, re, sys, collections
S = sys.argv[1]
rows = list(csv.reader(open(f"{S}/lizard.csv", encoding="utf-8")))
by = collections.defaultdict(list)
for r in rows:
    nloc, ccn, tok, par, length = map(int, r[:5]); f = r[6].replace("\\", "/")
    proj = "/".join(f.split("/")[:3])
    by[proj].append((ccn, nloc, par, length, r[7], f))
print("proj | metodos | nloc | ccn_medio | ccn_max | ccn>10 | ccn>5")
for p in sorted(by):
    m = by[p]; c = [x[0] for x in m]
    print(p, len(m), sum(x[1] for x in m), round(sum(c)/len(c), 2), max(c), sum(1 for x in c if x > 10), sum(1 for x in c if x > 5))
print("\nTOP CCN src")
for x in sorted([x for p in by if "/src/" in p for x in by[p]], reverse=True)[:15]:
    print(x[0], x[1], x[4], x[5])
for name in ("dup-src", "dup-tests"):
    t = open(f"{S}/{name}.txt", encoding="utf-8", errors="replace").read()
    blocks = t.split("Duplicate block:")[1:]
    cnt = collections.Counter(); dirs = collections.Counter(); lines = 0
    for b in blocks:
        for m in re.finditer(r"^(core/\S+?):(\d+) ~ (\d+)", b, re.M):
            f = m.group(1).replace("\\", "/"); cnt[f] += 1; dirs["/".join(f.split("/")[:3])] += 1
            lines += int(m.group(3)) - int(m.group(2)) + 1
    print(f"\n{name}: blocos={len(blocks)} ocorrencias={sum(cnt.values())} linhas_em_ocorrencias={lines}")
    for d, n in dirs.most_common(8): print("  ", n, d)
    for f, n in cnt.most_common(15): print("    ", n, f)
