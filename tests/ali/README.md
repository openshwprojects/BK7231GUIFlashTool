# ALi M3801 protocol selftests

Build the flasher, then run `powershell -File tests/run_ali_selftests.ps1`.
`-FlasherAssembly` selects a different built EXE. No NuGet test framework,
serial adapter, tuner, administrator access, or network is needed. CI runs
these checks against the release build before uploading its artifacts.

The test executable calls the production protocol and flasher adapter. Its
strict in-memory bootloader checks command order, decimal addresses, exact
1024-byte packets, independent bitwise CRCs, acknowledgements, and burn state.
Replies deliberately fragment at unrelated byte boundaries. It exercises
multiple updates, exclusive backups, full readback, rejection paths and
cancellation. These are host/protocol regression tests; they do not emulate
the MIPS CPU or prove compatibility with unrecognized tuner models.

`fixtures/maciej-loader.bin.gz` contains only bytes `0..0x4fdff` of the
authorized `dumps/dump_maciej.bin` from
[AliSimulator](https://github.com/openshwprojects/AliSimulator). It ends before
the HDCP chunk. The test constructs the HDCP and database chunks itself;
no per-device HDCP keys, configuration or databases are included. The two
569-byte maincode chunks contain the project's compiled v1 and v2 test app,
without the original firmware padding. The tests pad them with `0xff`.
`fixtures/manifest.json` records source and SHA-256 hashes. The loader fixture
is required to exercise the production loader fingerprint check unchanged.
