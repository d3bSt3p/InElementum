using System.Runtime.CompilerServices;

// Data setters are internal: authoring tools and tests write them, game code only reads.
[assembly: InternalsVisibleTo("InElementum.Editor")]
[assembly: InternalsVisibleTo("InElementum.Tests.EditMode")]
