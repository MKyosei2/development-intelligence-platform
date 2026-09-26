// Purpose: Exposes infrastructure internals only to the infrastructure test assembly for focused safety regression tests.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dip.Infrastructure.Tests")]
