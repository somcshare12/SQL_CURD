# Known limitations

* **No schema editing.** By design, the module manages records only — it never
  creates, alters, renames, truncates or drops schema objects.
* **Data grid.** A purpose-built, accessible, server-side grid is used rather
  than a third-party grid library. It supports sorting, filtering, column
  hide/show, sticky headers, keyboard row activation and layout persistence.
  Column reordering and drag-resize are persisted in the settings model but the
  drag interactions are not yet wired in the UI.
* **Foreign keys** display the raw key value with related-table info in the
  field description. A searchable FK lookup selector is an optional future
  enhancement (the module never loads an entire referenced table into a dropdown).
* **Bulk delete** is disabled by default and guarded (max rows, typed
  confirmation, single transaction). Bulk *update* is intentionally not offered.
* **No fake undo.** Deletes are permanent unless the database itself implements
  soft deletion.
* **Row-count on huge tables.** Exact counts use `COUNT_BIG(*)`; prefer
  `OnDemand`/`Estimated`/`Disabled` row-count modes for very large tables.
* **Estimated row count** currently falls back to an exact count for filtered
  queries; a statistics-based estimate is a future refinement.
* **OpenAPI transitive advisory.** A restore warning (`NU1903`) is emitted for a
  transitive `Microsoft.OpenApi` version pulled in by
  `Microsoft.AspNetCore.OpenApi`; it does not affect runtime behaviour.
