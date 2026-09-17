# Source cleanup correction record

This note records a corrective follow-up to commit
`d1b923a6569315bc52336a2222cd3c11219d52a2`.

That commit message described a broader cleanup than the committed patch actually
contained. The commit itself changed only the publication-readiness workflow,
the Purpose repair/verifier scripts, the audit repository contract, and
ProjectService tests. In particular, the committed patch did not include the
actual `ApiClient.Transport` source accessor, repository-wide formatting
results, repository-wide Purpose rewrites, or deletion of the obsolete
one-time cleanup scripts.

This corrective revision deliberately makes the implementation match the
claims:

- adds the real `ApiClient.Transport` accessor;
- extracts web-client transport DTOs from `ApiClient.cs`;
- applies repository-wide Purpose normalization;
- applies repository-wide C# formatting;
- removes obsolete one-time Apply/Patch scripts;
- tightens Development Graph evidence ingestion and filtered-edge correctness;
- rejects oversized version-control evidence instead of silently truncating it;
- fails closed on malformed Development Graph metadata;
- verifies readability in Strict mode before build/test completion.

No historical commit is relabeled as verified. Hosted GitHub Actions and a
GitHub Release remain separate exact-revision verification steps for the new
final SHA.
