# Server File Management

## Detail

As a user, I want to modify files on a cloud hosted game server.

This user story describes a core game-neutral hosting capability. Different game implementations may expose different important directories, classifications, and safety rules through the game-hosting abstraction, but the platform provides the common file operations. Arma 3 supplies the first such game-specific file capabilities; neither the generic file service nor the cloud storage/runtime layer should contain Arma 3-specific path rules.

Files are accessed as entries in a server-scoped folder-like tree. In the first iteration that tree is backed by the local filesystem under `{ConfiguredBasePath}/{ServerId}/...`.

The primary transport shape should be a file DTO with a server-relative path. The UI should support browsing and manipulating the tree, preferably with a Windows Explorer-like interaction model, but the exact UI component is less important than preserving the simple folder/file mental model.

Persistent browsing is an effective projection rather than a physical-directory listing. The API merges physical storage with virtual directories and generated configuration targets declared by the game implementation. Generated-only files are marked as generated and structured-editable. A physical file at the same path is returned once as a persistent override; structured editing for that target is hidden until the override is removed. Generated targets stay visible when incomplete. A physical directory conflicting with a generated-file target is a blocking conflict.

File management should expose two explicit modes instead of relying on hidden synchronization:

- **Persistent files**: the server's stored file tree used as source data for future starts.
- **Live cloud files**: the files currently present on the active cloud worker/runtime.

Synchronization between these modes occurs only at lifecycle boundaries: persistent/expected data is applied when a worker starts, and selected live data is preserved when a worker stops. The initial design does not provide a general-purpose apply, pull, or merge action between start and stop.

During Start preparation, persistent expected-file synchronization is the final operation before the game server's Start action is invoked. It runs after game installation/preparation, external Mod acquisition and generated Mod output, Scenario staging, and Configuration Profile rendering. A persistent expected file therefore overrides any generated or staged file at the same server-relative path. No later preparation stage may silently rewrite runtime files before Start.

Users with Server Modify should be able to CRUD persistent files while the Server is stopped. When a cloud runtime is active, both modes remain separately viewable. Live cloud files may be edited directly if the user has Server Modify and the game/runtime allows it. Persistent paths covered by a preservation selection are read-only while the deployment is active because Stop-time synchronization will replace them from the live worker. Persistent paths outside the preservation selection remain editable as expected data for the next Start because Stop-time synchronization will not overwrite them. A user with Server Modify can deliberately remove a preservation selection before editing its persistent path.

Each Server has a selection of specific live files or folders to preserve before an intentional worker deprovision. When a Server record is created, its game implementation seeds an initial set of preservation selections. Those server-relative selections persist with the Server configuration across deployments. Users with Server Modify can browse eligible paths and later add or remove allowed files or folders, including game-seeded selections. A later game-implementation update does not silently restore a path the user removed. Server Operate allows Stop/restart to use the saved selection but does not by itself expose or permit editing preservation paths. The generic platform does not know game-specific layouts. The Stop phase synchronizes only the selected paths back into persistent Server storage before removing the worker; it must not blindly download the entire runtime filesystem. Worker restart uses the same Stop behavior before its replacement starts and restores the updated persistent data.

Before selected-path synchronization, the worker asks the active game implementation to save and gracefully stop the game process. Each server record has configurable graceful-shutdown limits that can allow multiple shutdown requests within a maximum window. If the game process remains stuck after those limits expire, the worker force-terminates it. Synchronization does not begin while the game process is still running unless the game implementation has explicitly declared the selected files ready for safe preservation.

Selected-path synchronization still runs after forced game-process termination because the live worker is about to be removed. The operation records and exposes that the files were captured after a forced termination and may be internally inconsistent. The initial design does not automatically retain historical generations of synchronized data. A synchronization result is published as the new persistent state only after its transfer completes successfully; if transfer or publication fails, the previous persistent state remains intact. Temporary staging may be used to achieve this behavior but is not retained as user-visible history.

Preservation selections support three game-neutral scopes:

- **File**: synchronize one selected file.
- **Folder (flat)**: synchronize files directly inside the selected folder without traversing nested folders.
- **Folder (recursive)**: synchronize the selected folder and all descendant files and folders.

Folder selections are evaluated against live runtime contents when synchronization runs, so files created after the selection was configured are included when they match the chosen flat or recursive scope.

Each preservation selection has its own deletion-propagation policy. In create/update-only mode, a persistent file is not deleted merely because it is absent from the selected live path. In mirror-deletions mode, deleting a selected file from the live runtime also removes its persistent counterpart during synchronization. Different file and folder selections on the same server can therefore use different behavior. This data policy is distinct from user authorization permissions.

Manually added preservation selections default to create/update-only behavior so an accidental live deletion does not erase persistent data without an explicit choice. A game implementation may specify the intended deletion behavior for each selection it seeds through the game-hosting abstraction.

When multiple preservation selections match the same live path, the most specific selection determines deletion behavior. Specificity is ordered as recursive folder, then flat folder, then exact file match. Within the same selection scope, a deeper matching path is more specific than an ancestor path.

If this required synchronization fails or stalls during an ordinary user-requested Stop or worker restart, the deprovisioning phase waits for intervention while the existing worker remains allocated and potentially billable. There is no separate configurable preservation-transfer timeout for this ordinary path. A user with Server Operate can retry a failed transfer or invoke Terminate. Terminate is an extraordinary full-stop action available whenever the individual Server record has an active deployment and is terminal for that deployment's provisioned resources. It skips graceful game-process shutdown and preservation, interrupts active preservation, cancels all running and queued normal actions, discards any unpublished staged result, retains the previously published persistent state, and immediately begins verified full cost-stop with no replacement worker. It clearly warns that unsaved or unsynchronized live data may be lost, requires a mandatory typed Server-specific confirmation challenge, and records the actor and outcome. It is available through Server Operate, including the existing administrator bypass, rather than requiring a separate emergency claim.

Automatic, fault-triggered, cost-enforcement, and emergency cost-stop cannot wait indefinitely for file preservation. They make the configured bounded attempt to synchronize the selected paths, then proceed with deprovisioning if the worker is unavailable, synchronization fails, or the preservation window expires. The outcome records which selected data could not be preserved where that can be determined.

Mods may use the file system when they are uploaded file-based content, but modpacks and mods are not just raw files. They are tracked through the Mod/Modpack content model when they need versioning, client/server distinctions, Steam Workshop references, generated output, or user download tracking.

The file surface must respect game-specific safety rules. For example, Arma 3 may need mission files, profile files, mod configuration, server configuration, BattlEye files, and logs, while another game may expose a different set of paths.

For Arma 3 v1, launch-read Server Config, Basic Config, Server Profile, BattlEye startup configuration, and generated launch arguments are next-Start inputs rather than hot-editable live files. Logs and diagnostics are read-only. Supported live changes use projected Arma/BattlEye/RCon operations; ban/script files change live only through operations the integration can safely apply or reload. Scenario and Mod artifacts remain in reusable content storage rather than ordinary per-Server file editing.

## Definition Of Done

- [ ] A user can browse allowed files and directories for a server they can manage.
- [ ] A user can view and modify allowed configuration files.
- [ ] A user can upload files into allowed locations.
- [ ] A user can download files from allowed locations.
- [ ] File operations target server-relative paths instead of raw host paths.
- [ ] The first implementation stores each server's file tree on the local filesystem under `{ConfiguredBasePath}/{ServerId}/...`.
- [ ] The file DTO includes a server-relative path.
- [ ] The file DTO describes effective origin, generated target, override state, structured-edit capability, and configuration completeness.
- [ ] Browsing a virtual generated directory succeeds even when no corresponding physical directory exists.
- [ ] Physical and generated collisions produce one effective entry, with the physical file winning.
- [ ] Removing a physical override immediately reveals the generated target.
- [ ] The UI supports a folder-like browsing and operation model.
- [ ] The UI distinguishes persistent files from live cloud files.
- [ ] Start applies persistent/expected server data to the worker, and stop preserves only selected live data back into persistent storage.
- [ ] Start applies persistent expected files after game/Mod preparation and Configuration Profile rendering.
- [ ] Persistent expected-file synchronization is the final file-preparation operation before the game Start action.
- [ ] Persistent expected files override matching generated configuration and Mod-related runtime files.
- [ ] No general-purpose synchronization or merge action between the two modes is required outside start and stop.
- [ ] Users can CRUD persistent files through the storage abstraction.
- [ ] Users can CRUD live cloud files when a runtime is active and the game/runtime allows it.
- [ ] While a deployment is active, persistent paths covered by a preservation selection are viewable but read-only.
- [ ] While a deployment is active, persistent paths outside preservation selections remain editable as expected data for the next start.
- [ ] Live worker paths remain separately editable where permissions and game/runtime rules allow it.
- [ ] The system does not imply automatic synchronization between persistent files and live cloud files.
- [ ] Each server can retain a selection of server-relative live files or folders to synchronize before intentional worker deprovisioning.
- [ ] A game implementation seeds initial preservation selections when the server record is created without exposing game-specific paths to the generic platform.
- [ ] Preservation selections persist with the server configuration across deployments.
- [ ] Users can manually add or remove allowed files or folders, including game-seeded selections.
- [ ] Viewing or editing preservation selections requires Server Modify.
- [ ] Server Operate can run Stop/restart using the saved selection but does not itself grant access to inspect or change file paths.
- [ ] Game-implementation updates do not silently re-add preservation selections that a user removed.
- [ ] A preservation selection can target one file, a folder's direct files only, or a folder recursively.
- [ ] Flat folder selections do not traverse nested folders.
- [ ] Recursive folder selections include all descendant files and folders.
- [ ] Folder selections are evaluated at synchronization time so newly created matching files are preserved.
- [ ] Selected-path synchronization supports create/update-only and mirror-deletions behavior.
- [ ] Deletion propagation is configured independently for each preservation selection.
- [ ] Manually added preservation selections default to create/update only.
- [ ] A game implementation can specify deletion behavior for each game-seeded selection.
- [ ] Overlapping preservation selections resolve deletion behavior using the most specific match.
- [ ] Specificity precedence is recursive folder, then flat folder, then exact file match, with deeper paths winning within the same scope.
- [ ] Create/update-only synchronization retains persistent files that are absent from the live runtime.
- [ ] Mirror-deletions synchronization can remove a persistent counterpart when the selected live file was deleted.
- [ ] Deletion propagation is modeled as synchronization behavior rather than an authorization permission.
- [ ] Pre-deprovision synchronization copies only selected paths rather than blindly copying the entire runtime filesystem.
- [ ] The game process is asked to save and stop gracefully before selected-path synchronization begins.
- [ ] Graceful-shutdown attempts and maximum wait are configurable for each server record and can permit repeated shutdown requests.
- [ ] A game process still running after the configured graceful-shutdown limits is force-terminated.
- [ ] Selected paths are not synchronized while the game process is running unless the game implementation explicitly declares them ready.
- [ ] Selected paths are still synchronized after forced game-process termination, and the result is marked as potentially inconsistent.
- [ ] A synchronization result replaces persistent state only after the transfer completes successfully.
- [ ] Failed synchronization leaves the previous persistent state intact rather than publishing a partial update.
- [ ] The initial design does not retain automatic historical generations of synchronized data.
- [ ] Worker restart synchronizes those paths before removing the old worker and restores the resulting persistent data onto the replacement.
- [ ] A failed synchronization pauses an ordinary user-requested stop or worker restart while the existing worker remains allocated and potentially billable.
- [ ] Ordinary preservation transfer has no separate configurable timeout; a slow or stalled transfer remains visible until it completes, fails, or the user invokes Terminate.
- [ ] Users with Server Operate can invoke Terminate after a clear data-loss warning; the actor and outcome are audited.
- [ ] Terminate requires a mandatory typed server-specific confirmation challenge.
- [ ] Terminate is available whenever the individual server record has an active deployment.
- [ ] Terminate skips graceful game shutdown and file preservation and immediately tears down provisioned deployment resources.
- [ ] Terminate bypasses the normal queue, interrupts active preservation, and cancels all running and queued normal actions.
- [ ] Terminate discards unpublished staged data, retains the previous persistent state, and proceeds to verified full cost-stop with no replacement worker.
- [ ] Terminate does not delete persistent server data that was published before it was invoked.
- [ ] Automatic, fault-triggered, cost-enforcement, and emergency cost-stop proceed after a bounded preservation attempt and record unrecoverable selected data where possible.
- [ ] The system can classify files or paths as game files, save files, logs, configs, mods, missions, uploads, generated files, or downloaded files.
- [ ] File operations are audited with actor, server, path, action, and timestamp.
- [ ] Forbidden paths, secrets, credentials, provider tokens, worker credentials, and host system files are not exposed.
- [ ] Game-specific file areas are described through game capability metadata instead of hard-coded UI assumptions.
- [ ] Arma 3 file rules are supplied by the Arma 3 implementation and do not leak into generic storage or cloud/runtime contracts.

## Assumptions

- File access should be scoped to a server and mediated by the platform, not by direct SSH.
- Local filesystem is the only required backend for the first implementation.
- Some files may require the server to be stopped before editing.
- File classification is separate from physical storage.
- Persistent-file CRUD and live-cloud-file CRUD are separate user-visible modes.
- Lifecycle-only synchronization and active-deployment read-only rules avoid requiring file merge or conflict-resolution behavior.
- Automatic pre-deprovision synchronization is limited to the server's selected preservation paths and does not make all live and persistent files implicitly synchronized.
- Backup/version history is a separate future capability with its own storage policy rather than an implicit part of stop-time synchronization.

## Open Questions

- Is an in-browser text editor required for v1, or is upload/download enough initially?
- Do users need archive upload/extract support for mod packs or missions?
- Should file changes automatically create backups or restore points?
- Which File DTO fields are required for v1?
- Which file classifications are required for v1?
- How should mod files be represented when they may be uploaded for one game but downloaded for another?
