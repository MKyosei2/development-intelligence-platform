#!/usr/bin/env python3
# Purpose: Provides v al id at e c om mi t m es sa ge tooling for repository verification and development workflows.
import re, sys
RX=re.compile(r'^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-z0-9._/-]+\))?!?: .{3,72}$')
def valid(s): return bool(RX.match(s)) or s.startswith('Merge ') or s.startswith('Revert ')
msgs=[x.strip() for x in sys.stdin if x.strip()]
bad=[x for x in msgs if not valid(x)]
if bad:
 print('Invalid commit subject(s):',file=sys.stderr)
 for x in bad: print(' - '+x,file=sys.stderr)
 print('Expected e.g. feat(qa): add runtime defect evidence',file=sys.stderr); raise SystemExit(2)
print(f'PASS: {len(msgs)} commit subject(s) follow the repository convention.')
