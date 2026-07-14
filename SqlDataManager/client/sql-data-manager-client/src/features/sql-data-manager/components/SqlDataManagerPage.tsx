import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { NavigationRail } from './NavigationRail';
import { ObjectTree } from './ObjectTree';
import { DataGrid } from '../grid/DataGrid';
import { RecordForm } from '../forms/RecordForm';
import { DeleteDialog } from '../dialogs/DeleteDialog';
import { RecordDetailsDrawer } from '../dialogs/RecordDetailsDrawer';
import { ThemeToggle } from './ThemeToggle';
import { useMetadata, useModuleInfo, useObjects } from '../hooks/queries';
import { useSqlDataManager } from '../providers/SqlDataManagerProvider';
import { makeRoutes } from '../utilities/routing';
import type { DatabaseObject, Row, SqlDataManagerPageProps } from '../models';

type DialogState =
  | { kind: 'none' }
  | { kind: 'create' }
  | { kind: 'edit'; row: Row }
  | { kind: 'delete'; row: Row }
  | { kind: 'details'; row: Row };

/**
 * The primary entry component. In standalone mode it renders the full shell
 * (header, navigation rail, object tree, grid, dialogs). In embedded mode the
 * chrome pieces can be switched off via props so it slots into a parent shell
 * without creating a second application frame.
 */
export function SqlDataManagerPage({
  showHeader = true,
  showNavigationRail = true,
  showObjectTree = true,
  showConnectionStatus = true,
  embedded = false,
  onObjectSelected,
}: SqlDataManagerPageProps) {
  const { basePath } = useSqlDataManager();
  const routes = useMemo(() => makeRoutes(basePath), [basePath]);
  const navigate = useNavigate();
  const params = useParams();

  const [treeOpen, setTreeOpen] = useState(showObjectTree);
  const [dialog, setDialog] = useState<DialogState>({ kind: 'none' });
  const [toast, setToast] = useState<string | null>(null);

  const info = useModuleInfo();
  const objectsQuery = useObjects();

  const schema = params.schema;
  const objectName = params.objectName;
  const metadataQuery = useMetadata(schema, objectName);

  const selectedObject = objectsQuery.data?.find((o) => o.schema === schema && o.name === objectName);

  const showToast = useCallback((message: string) => {
    setToast(message);
    window.setTimeout(() => setToast(null), 3500);
  }, []);

  useEffect(() => {
    if (selectedObject) onObjectSelected?.(selectedObject);
  }, [selectedObject, onObjectSelected]);

  function selectObject(object: DatabaseObject) {
    navigate(routes.object(object));
  }

  const rootClass = `sdm-root ${embedded ? 'sdm-root--embedded' : ''}`;

  return (
    <div className={rootClass}>
      {showHeader && (
        <header className="sdm-header">
          <div className="sdm-header__title">
            <span className="sdm-header__logo">🗄</span>
            <strong>{info.data?.displayName ?? 'SQL Data Manager'}</strong>
          </div>
          <div className="sdm-header__actions">
            {showConnectionStatus && (
              <ConnectionStatus databaseName={info.data?.databaseDisplayName} failed={info.isError} />
            )}
            {!embedded && <ThemeToggle />}
          </div>
        </header>
      )}

      <div className="sdm-body">
        {showNavigationRail && <NavigationRail treeOpen={treeOpen} onToggleTree={() => setTreeOpen((v) => !v)} />}

        {showObjectTree && treeOpen && (
          <div className="sdm-tree-panel">
            <ObjectTree
              objects={objectsQuery.data ?? []}
              selectedFullName={selectedObject?.fullName}
              onSelect={selectObject}
              loading={objectsQuery.isLoading}
              error={objectsQuery.isError ? 'Could not load database objects.' : undefined}
            />
          </div>
        )}

        <main className="sdm-content">
          {!selectedObject && (
            <div className="sdm-empty-state">
              <h2>Select a table or view</h2>
              <p>Choose an object from the tree on the left to browse and manage its records.</p>
            </div>
          )}

          {selectedObject && metadataQuery.isLoading && <div className="sdm-loading">Loading metadata…</div>}
          {selectedObject && metadataQuery.isError && (
            <div className="sdm-loading sdm-loading--error">Could not load metadata for this object.</div>
          )}

          {selectedObject && metadataQuery.data && info.data && (
            <>
              <div className="sdm-content__title">
                <h1>{metadataQuery.data.fullName}</h1>
                <span className="sdm-content__type">{metadataQuery.data.objectType}</span>
                {!metadataQuery.data.hasStableKey && (
                  <span className="sdm-content__nokey" title="No stable key: update/delete disabled">
                    no key
                  </span>
                )}
              </div>

              <DataGrid
                key={metadataQuery.data.fullName}
                metadata={metadataQuery.data}
                info={info.data}
                onCreate={() => setDialog({ kind: 'create' })}
                onRowSelected={(row) => setDialog({ kind: 'details', row })}
              />
            </>
          )}
        </main>

        {dialog.kind === 'details' && metadataQuery.data && (
          <RecordDetailsDrawer
            metadata={metadataQuery.data}
            record={dialog.row}
            onClose={() => setDialog({ kind: 'none' })}
            onEdit={metadataQuery.data.permissions.canUpdate ? () => setDialog({ kind: 'edit', row: dialog.row }) : undefined}
            onDelete={metadataQuery.data.permissions.canDelete ? () => setDialog({ kind: 'delete', row: dialog.row }) : undefined}
          />
        )}
      </div>

      {/* Modal dialogs */}
      {dialog.kind === 'create' && metadataQuery.data && (
        <RecordForm
          metadata={metadataQuery.data}
          mode="create"
          onClose={() => setDialog({ kind: 'none' })}
          onCompleted={showToast}
        />
      )}
      {dialog.kind === 'edit' && metadataQuery.data && (
        <RecordForm
          metadata={metadataQuery.data}
          mode="edit"
          initialRecord={dialog.row}
          onClose={() => setDialog({ kind: 'none' })}
          onCompleted={showToast}
        />
      )}
      {dialog.kind === 'delete' && metadataQuery.data && (
        <DeleteDialog
          metadata={metadataQuery.data}
          record={dialog.row}
          onClose={() => setDialog({ kind: 'none' })}
          onCompleted={showToast}
        />
      )}

      {toast && (
        <div className="sdm-toast" role="status" aria-live="polite">
          {toast}
        </div>
      )}
    </div>
  );
}

function ConnectionStatus({ databaseName, failed }: { databaseName?: string; failed: boolean }) {
  return (
    <div className={`sdm-connection ${failed ? 'is-error' : 'is-ok'}`} title="Database connection status">
      <span className="sdm-connection__dot" aria-hidden />
      {failed ? 'Connection error' : databaseName ? `Connected · ${databaseName}` : 'Connecting…'}
    </div>
  );
}
