import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import {
  HostingAuditFeed,
  hostingAuditTone,
  parseHostingAuditDetail,
} from "@/features/hosting/HostingAuditFeed";
import type { HostingOperation } from "@/features/hosting/types";

const baseOperation: HostingOperation = {
  primaryKey: "audit-1",
  kind: "LifecycleRequested",
  status: "pending",
  summary: "Lifecycle action requested",
  detail: null,
  providerIdentifier: null,
  resourceKind: null,
  resourceIdentifier: null,
  providerState: null,
  deploymentId: null,
  lifecycleJobId: null,
  workerId: null,
  traceId: null,
  spanId: null,
  timeStampCreated: "2026-08-13T10:05:00Z",
};

function operation(overrides: Partial<HostingOperation>): HostingOperation {
  return { ...baseOperation, ...overrides };
}

function renderFeed(operations: HostingOperation[], compact = false, loading = false) {
  return render(
    <I18nextProvider i18n={i18n}>
      <HostingAuditFeed operations={operations} compact={compact} loading={loading} />
    </I18nextProvider>,
  );
}

describe("hosting audit feed", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("maps operational outcomes to meaningful status tones", () => {
    expect(hostingAuditTone(operation({ status: "failed" }))).toBe("danger");
    expect(hostingAuditTone(operation({ status: "warning" }))).toBe("warning");
    expect(hostingAuditTone(operation({ status: "pending" }))).toBe("info");
    expect(hostingAuditTone(operation({ status: "succeeded" }))).toBe("success");
  });

  it("separates machine detail from narrative and failure evidence", () => {
    expect(
      parseHostingAuditDetail(
        "Provider=hetzner; Kind=primary-ip; Identity=ip-12; Stage=create; Failure=rate-limited; Attempt=2",
      ),
    ).toEqual({
      narrative: null,
      failure: "rate-limited",
      fields: [
        { label: "Provider", value: "hetzner" },
        { label: "Kind", value: "primary-ip" },
        { label: "Identity", value: "ip-12" },
        { label: "Stage", value: "create" },
        { label: "Attempt", value: "2" },
      ],
    });
    expect(parseHostingAuditDetail("The worker recovered without intervention.")).toEqual({
      narrative: "The worker recovered without intervention.",
      failure: null,
      fields: [],
    });
    expect(
      parseHostingAuditDetail(
        "FaultCode=worker-timeout; Fault=Worker heartbeat expired; Reason=automatic cost stop",
      ),
    ).toEqual({
      narrative: null,
      failure: "Worker heartbeat expired",
      fields: [
        { label: "Fault Code", value: "worker-timeout" },
        { label: "Reason", value: "automatic cost stop" },
      ],
    });
  });

  it("renders a date-grouped audit narrative with hidden technical evidence", async () => {
    const user = userEvent.setup();
    const failed = operation({
      primaryKey: "audit-failed",
      kind: "DeploymentResourceProvisioningFailed",
      status: "failed",
      summary: "Public address allocation failed",
      detail:
        "Provider=hetzner; Kind=primary-ip; Identity=ip-12; Stage=create; Failure=rate-limited; Attempt=2",
      providerIdentifier: "hetzner",
      resourceKind: "primary-ip",
      resourceIdentifier: "ip-12",
      providerState: "create",
      deploymentId: "deployment-1",
      lifecycleJobId: "job-1",
      workerId: "worker-1",
      traceId: "trace-1",
      spanId: "span-1",
      timeStampCreated: "2026-08-14T12:05:00Z",
    });
    const recovered = operation({
      primaryKey: "audit-recovered",
      kind: "WorkerRecovered",
      status: "succeeded",
      summary: "Worker connection recovered",
      detail: "The worker recovered without intervention.",
      providerIdentifier: "hetzner",
      timeStampCreated: "2026-08-13T10:05:00Z",
    });

    renderFeed([recovered, failed]);

    const entries = screen.getAllByRole("listitem");
    expect(entries).toHaveLength(2);
    const failedEntry = entries[0]!;
    const recoveredEntry = entries[1]!;
    expect(within(failedEntry).getByRole("heading", { name: failed.summary })).toBeVisible();
    expect(within(recoveredEntry).getByRole("heading", { name: recovered.summary })).toBeVisible();
    const recordLabel = screen.getByText("audit records");
    expect(within(recordLabel.parentElement as HTMLElement).getByText("2")).toBeVisible();
    expect(recordLabel).toBeVisible();
    expect(screen.getByText("Newest first")).toBeVisible();

    const timestamp = within(failedEntry)
      .getByText(/\d{1,2}:\d{2}/)
      .closest("time");
    expect(timestamp).toHaveAttribute("datetime", failed.timeStampCreated);
    expect(within(failedEntry).getByText("Source")).toBeVisible();
    expect(within(failedEntry).getByText("hetzner")).toBeVisible();
    expect(within(failedEntry).getByText("Resource type")).toBeVisible();
    expect(within(failedEntry).getByText("Primary ip")).toBeVisible();
    expect(within(failedEntry).getByText("Provider state")).toBeVisible();
    expect(within(failedEntry).getByText("Create")).toBeVisible();
    expect(within(failedEntry).getByText("rate-limited")).toBeVisible();
    expect(screen.getByText("The worker recovered without intervention.")).toBeVisible();
    expect(
      screen.queryByText(
        "Provider=hetzner; Kind=primary-ip; Identity=ip-12; Stage=create; Failure=rate-limited; Attempt=2",
      ),
    ).not.toBeInTheDocument();

    const evidence = within(failedEntry).getByText("Technical evidence").closest("details");
    expect(evidence).not.toHaveAttribute("open");
    expect(within(evidence as HTMLElement).getByText("deployment-1")).not.toBeVisible();
    await user.click(within(failedEntry).getByText("Technical evidence"));
    expect(evidence).toHaveAttribute("open");
    expect(within(evidence as HTMLElement).getByText("audit-failed")).toBeVisible();
    expect(within(evidence as HTMLElement).getByText("deployment-1")).toBeVisible();
    expect(within(evidence as HTMLElement).getByText("job-1")).toBeVisible();
    expect(within(evidence as HTMLElement).getByText("worker-1")).toBeVisible();
    expect(within(evidence as HTMLElement).getByText("trace-1")).toBeVisible();
    expect(within(evidence as HTMLElement).getByText("span-1")).toBeVisible();
    expect(within(evidence as HTMLElement).getByText("Attempt")).toBeVisible();
  });

  it("surfaces recovery faults as warning callouts instead of raw evidence", () => {
    renderFeed([
      operation({
        kind: "Recovery",
        status: "warning",
        summary: "Automatic provider cost-stop queued",
        detail: "FaultCode=worker-timeout; Fault=Worker heartbeat expired",
      }),
    ]);

    const notice = screen.getByText("Worker heartbeat expired").closest("div");
    expect(notice).toHaveClass("next-hosting-audit-notice");
    expect(notice).toHaveAttribute("data-tone", "warning");
    expect(screen.getByText("Fault Code")).not.toBeVisible();
  });

  it("reuses the compact entry design without exposing correlation identifiers", () => {
    renderFeed(
      [
        operation({
          traceId: "trace-hidden",
          summary: "Compact activity",
          providerIdentifier: "control",
        }),
      ],
      true,
    );

    expect(screen.getByRole("heading", { name: "Compact activity" })).toBeVisible();
    expect(screen.queryByText("Technical evidence")).not.toBeInTheDocument();
    expect(screen.queryByText("trace-hidden")).not.toBeInTheDocument();
  });

  it("uses resource-backed empty and German audit labels", async () => {
    await i18n.changeLanguage("de");
    const { rerender } = renderFeed([]);
    expect(
      screen.getByText("Für diesen Server wurden noch keine Betriebsaktivitäten aufgezeichnet."),
    ).toBeVisible();

    rerender(
      <I18nextProvider i18n={i18n}>
        <HostingAuditFeed operations={[baseOperation]} />
      </I18nextProvider>,
    );
    expect(screen.getByText("Neueste zuerst")).toBeVisible();
    expect(screen.getByText("Steuerungsebene")).toBeVisible();
    expect(screen.getByText("Technische Nachweise")).toBeVisible();
    expect(screen.getByText("Lifecycle")).toBeVisible();
    expect(screen.getByText("In Bearbeitung")).toBeVisible();
  });

  it("distinguishes loading from an empty audit history", () => {
    renderFeed([], false, true);

    expect(screen.getByText("Loading operational activity…")).toBeVisible();
    expect(
      screen.queryByText("No operational activity has been recorded for this server."),
    ).not.toBeInTheDocument();
  });
});
