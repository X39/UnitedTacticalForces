import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach, vi } from "vitest";
import { FakeWebSocket } from "./helpers/fakeWebSocket";
import { seedSettingsPanels } from "./helpers/settingsPanels";

HTMLDialogElement.prototype.showModal ??= function showModal() {
  this.open = true;
};
HTMLDialogElement.prototype.close ??= function close() {
  this.open = false;
};

// Every test file runs against the deterministic WebSocket double, so layouts and pages never open a real socket.
vi.stubGlobal("WebSocket", FakeWebSocket);
afterEach(() => FakeWebSocket.reset());

// Existing Settings flows render with every panel open, so their content is reachable. The collapsed first-visit
// default is tested explicitly: those tests call clearSettingsPanels() before they render.
beforeEach(() => seedSettingsPanels());
afterEach(() => sessionStorage.clear());
