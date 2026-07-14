import { useMemo, useState } from 'react';
import type { DatabaseObject, DatabaseObjectType } from '../models';

interface ObjectTreeProps {
  objects: DatabaseObject[];
  selectedFullName?: string;
  onSelect: (object: DatabaseObject) => void;
  loading?: boolean;
  error?: string;
}

interface SchemaGroup {
  schema: string;
  objects: DatabaseObject[];
}

/**
 * The searchable left navigation tree. Objects are grouped first by type
 * (Tables / Views) and then by schema, matching the required hierarchy. Search
 * matches schema name, object name and fully-qualified name, and automatically
 * reveals matching branches by keeping their groups expanded.
 */
export function ObjectTree({ objects, selectedFullName, onSelect, loading, error }: ObjectTreeProps) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return objects;
    return objects.filter(
      (o) =>
        o.name.toLowerCase().includes(term) ||
        o.schema.toLowerCase().includes(term) ||
        o.fullName.toLowerCase().includes(term),
    );
  }, [objects, search]);

  const tables = useMemo(() => groupBySchema(filtered, 'Table'), [filtered]);
  const views = useMemo(() => groupBySchema(filtered, 'View'), [filtered]);

  return (
    <div className="sdm-tree" role="tree" aria-label="Database objects">
      <div className="sdm-tree__search">
        <input
          type="search"
          placeholder="Search tables and views…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search database objects"
        />
      </div>

      {loading && <p className="sdm-tree__status">Loading objects…</p>}
      {error && (
        <p className="sdm-tree__status sdm-tree__status--error" role="alert">
          {error}
        </p>
      )}

      {!loading && !error && (
        <div className="sdm-tree__body">
          <TreeSection
            title="Tables"
            groups={tables}
            selectedFullName={selectedFullName}
            onSelect={onSelect}
            forceExpanded={Boolean(search)}
          />
          <TreeSection
            title="Views"
            groups={views}
            selectedFullName={selectedFullName}
            onSelect={onSelect}
            forceExpanded={Boolean(search)}
          />
          {filtered.length === 0 && <p className="sdm-tree__status">No matching objects.</p>}
        </div>
      )}
    </div>
  );
}

function TreeSection({
  title,
  groups,
  selectedFullName,
  onSelect,
  forceExpanded,
}: {
  title: string;
  groups: SchemaGroup[];
  selectedFullName?: string;
  onSelect: (o: DatabaseObject) => void;
  forceExpanded: boolean;
}) {
  if (groups.length === 0) return null;
  const total = groups.reduce((sum, g) => sum + g.objects.length, 0);

  return (
    <section className="sdm-tree__section">
      <h3 className="sdm-tree__section-title">
        {title} <span className="sdm-tree__count">{total}</span>
      </h3>
      {groups.map((group) => (
        <SchemaNode
          key={`${title}-${group.schema}`}
          group={group}
          selectedFullName={selectedFullName}
          onSelect={onSelect}
          forceExpanded={forceExpanded}
        />
      ))}
    </section>
  );
}

function SchemaNode({
  group,
  selectedFullName,
  onSelect,
  forceExpanded,
}: {
  group: SchemaGroup;
  selectedFullName?: string;
  onSelect: (o: DatabaseObject) => void;
  forceExpanded: boolean;
}) {
  const [expanded, setExpanded] = useState(true);
  const isOpen = forceExpanded || expanded;

  return (
    <div className="sdm-tree__schema" role="group">
      <button
        type="button"
        className="sdm-tree__schema-toggle"
        aria-expanded={isOpen}
        onClick={() => setExpanded((v) => !v)}
      >
        <span className={`sdm-tree__chevron ${isOpen ? 'is-open' : ''}`} aria-hidden>
          ▸
        </span>
        {group.schema}
        <span className="sdm-tree__count">{group.objects.length}</span>
      </button>

      {isOpen && (
        <ul className="sdm-tree__list">
          {group.objects.map((o) => {
            const selected = o.fullName === selectedFullName;
            return (
              <li key={o.fullName} role="none">
                <button
                  type="button"
                  role="treeitem"
                  aria-selected={selected}
                  title={o.fullName}
                  className={`sdm-tree__item ${selected ? 'is-selected' : ''}`}
                  onClick={() => onSelect(o)}
                >
                  <span className="sdm-tree__item-icon" aria-hidden>
                    {o.objectType === 'View' ? '◫' : '▦'}
                  </span>
                  <span className="sdm-tree__item-label">{o.name}</span>
                  {!o.permissions.canCreate && !o.permissions.canUpdate && !o.permissions.canDelete && (
                    <span className="sdm-tree__badge" title="Read only">
                      RO
                    </span>
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function groupBySchema(objects: DatabaseObject[], type: DatabaseObjectType): SchemaGroup[] {
  const bySchema = new Map<string, DatabaseObject[]>();
  for (const o of objects.filter((x) => x.objectType === type)) {
    const list = bySchema.get(o.schema) ?? [];
    list.push(o);
    bySchema.set(o.schema, list);
  }
  return [...bySchema.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([schema, objs]) => ({ schema, objects: objs.sort((a, b) => a.name.localeCompare(b.name)) }));
}
