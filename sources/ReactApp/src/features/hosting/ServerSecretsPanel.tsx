import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { StatePanel } from "@/components/primitives";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";

export function ServerSecretsPanel({ server }: { server: GameServer }) {
  const client = useQueryClient();
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [revealed, setRevealed] = useState<Record<string, string>>({});
  const statuses = useQuery({
    queryKey: hostingQueryKeys.secrets(server.primaryKey),
    queryFn: ({ signal }) => hostingService.secrets(server.primaryKey, signal),
    enabled: server.canManageSecrets,
  });
  const refresh = () =>
    client.invalidateQueries({ queryKey: hostingQueryKeys.secrets(server.primaryKey) });
  const setSecret = useMutation({
    mutationFn: ({
      key,
      value,
      externalProvider,
    }: {
      key: string;
      value: string;
      externalProvider: boolean;
    }) => hostingService.setSecret(server.primaryKey, key, value, externalProvider),
    onSuccess: async (_, { key }) => {
      setDrafts((current) => ({ ...current, [key]: "" }));
      await refresh();
    },
  });
  const rotate = useMutation({
    mutationFn: (key: string) => hostingService.rotateSecret(server.primaryKey, key),
    onSuccess: refresh,
  });
  const reveal = useMutation({
    mutationFn: (key: string) => hostingService.revealSecret(server.primaryKey, key),
    onSuccess: (value) => setRevealed((current) => ({ ...current, [value.key]: value.value })),
  });

  if (!server.canManageSecrets)
    return (
      <StatePanel kind="forbidden">
        You do not have permission to manage server credentials.
      </StatePanel>
    );
  if (statuses.isLoading) return <StatePanel kind="loading">Loading credentials…</StatePanel>;
  if (statuses.isError) return <StatePanel kind="error">{statuses.error.message}</StatePanel>;

  return (
    <section className="next-hosting-panel">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">Security</span>
          <h2>Server credentials</h2>
        </div>
        <p>Values are encrypted at rest. Reveal and rotation actions are audited.</p>
      </header>
      {(statuses.data ?? []).map((secret) => (
        <article className="next-hosting-storage-fit" key={secret.key}>
          <strong>{secret.displayName}</strong>
          <span>{secret.isConfigured ? `Configured · version ${secret.version}` : "Missing"}</span>
          {secret.requiredForStart && !secret.isConfigured && (
            <StatePanel kind="warning">Required before this server can start.</StatePanel>
          )}
          {!secret.generated && (
            <label className="field">
              <span>Set a new value</span>
              <input
                type="password"
                autoComplete="new-password"
                value={drafts[secret.key] ?? ""}
                onChange={(event) =>
                  setDrafts((current) => ({ ...current, [secret.key]: event.target.value }))
                }
              />
            </label>
          )}
          {revealed[secret.key] && <code>{revealed[secret.key]}</code>}
          <div className="next-hosting-form-actions">
            {!secret.generated && (
              <button
                className="button button--primary"
                type="button"
                disabled={!drafts[secret.key] || setSecret.isPending}
                onClick={() =>
                  setSecret.mutate({
                    key: secret.key,
                    value: drafts[secret.key] ?? "",
                    externalProvider: secret.externalProvider,
                  })
                }
              >
                Save
              </button>
            )}
            {secret.generated && (
              <button
                className="button button--ghost"
                type="button"
                disabled={rotate.isPending}
                onClick={() => rotate.mutate(secret.key)}
              >
                Rotate
              </button>
            )}
            {server.canRevealSecrets && secret.revealable && secret.isConfigured && (
              <button
                className="button button--ghost"
                type="button"
                disabled={reveal.isPending}
                onClick={() => reveal.mutate(secret.key)}
              >
                Reveal
              </button>
            )}
          </div>
        </article>
      ))}
    </section>
  );
}
