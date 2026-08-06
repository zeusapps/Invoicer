# FA(3) schema closure

Unmodified copies of the official KSeF FA(3) schema and everything it references,
published by the Centralne Repozytorium Wzorów Dokumentów Elektronicznych.
Used by `KsefSchemaValidator` to validate generated XML offline.

Retrieved 2026-08-06:

| File | Source URL | SHA-256 |
| --- | --- | --- |
| `schemat.xsd` | https://crd.gov.pl/wzor/2025/06/25/13775/schemat.xsd | `b646b6b525f51adf1bb2545f111fc8ca6e7aa6dd2f98948f1667d3695c06d958` |
| `StrukturyDanych_v10-0E.xsd` | http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/StrukturyDanych_v10-0E.xsd | `1137ce6e3c11c2b9ef3f05e4e72d6dcd6b4fa94908ea558f2ba15de0259bb2aa` |
| `ElementarneTypyDanych_v10-0E.xsd` | http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/ElementarneTypyDanych_v10-0E.xsd | `8a531cb181d3e298d11b28766655ae91fee2d7851440095932ffc82137ed2be1` |
| `KodyKrajow_v10-0E.xsd` | http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/KodyKrajow_v10-0E.xsd | `1d41a1b3184188f2d20a51d3afde26204dda182ec5dacf018204dcc9870dc644` |

Reference graph — `schemat.xsd` imports the `eD/DefinicjeTypy` namespace, which then
includes its way down to the country codes:

```
schemat.xsd  ──import──▶ StrukturyDanych_v10-0E.xsd
                            └─include──▶ ElementarneTypyDanych_v10-0E.xsd
                                            └─include──▶ KodyKrajow_v10-0E.xsd
```

## Refreshing

Do not edit these files. The `schemaLocation` attributes are absolute `crd.gov.pl`
URLs and are meant to stay that way — `LocalSchemaResolver` maps them to the copies
in this folder, so provenance stays verifiable and refreshing is a re-download
rather than a re-patch:

```bash
curl -L -o schemat.xsd https://crd.gov.pl/wzor/2025/06/25/13775/schemat.xsd
# ...and the three DefinicjeTypy files from the URLs above
```

A new FA wzór changes the target namespace, which makes it a new schema rather than
a refresh of this one — that belongs in its own change, alongside the metadata
constants in `KsefXmlGenerator.Metadata`.
