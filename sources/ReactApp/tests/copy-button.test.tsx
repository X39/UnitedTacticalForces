import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach } from "vitest";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { CopyButton } from "@/components/CopyButton";

const failedText = "Copying failed. Select the text and copy it yourself.";
const originalClipboard = Object.getOwnPropertyDescriptor(navigator, "clipboard");

/** Replaces `navigator.clipboard`; passing undefined simulates a missing clipboard (insecure context). */
function setClipboard(clipboard: Pick<Clipboard, "writeText"> | undefined) {
  Object.defineProperty(navigator, "clipboard", { value: clipboard, configurable: true });
}

function renderButton() {
  return render(
    <I18nextProvider i18n={i18n}>
      <CopyButton
        value="10.0.0.1:2302"
        label="Copy address"
        copiedMessage="Server address copied."
      />
    </I18nextProvider>,
  );
}

/** Lets the pending clipboard promise settle inside act. */
async function flush() {
  await act(async () => {
    await Promise.resolve();
  });
}

describe("CopyButton", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    if (originalClipboard) Object.defineProperty(navigator, "clipboard", originalClipboard);
    else Reflect.deleteProperty(navigator, "clipboard");
  });

  it("writes the value on click, confirms for two seconds and announces the caller message", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    setClipboard({ writeText });
    renderButton();

    fireEvent.click(screen.getByRole("button", { name: "Copy address" }));
    await flush();

    expect(writeText).toHaveBeenCalledExactlyOnceWith("10.0.0.1:2302");
    expect(screen.getByRole("button", { name: "Copied" })).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Server address copied.");

    act(() => {
      vi.advanceTimersByTime(1999);
    });
    expect(screen.getByRole("button", { name: "Copied" })).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(screen.getByRole("button", { name: "Copy address" })).toBeInTheDocument();
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("shows the visible failure text and throws nothing when the write is denied", async () => {
    const denied = new DOMException("denied", "NotAllowedError");
    setClipboard({ writeText: vi.fn().mockRejectedValue(denied) });
    renderButton();

    fireEvent.click(screen.getByRole("button", { name: "Copy address" }));
    await flush();

    expect(screen.getByRole("status")).toHaveTextContent(failedText);
    expect(screen.getByRole("button", { name: "Copy address" })).toBeInTheDocument();
  });

  it("shows the visible failure text when navigator.clipboard is missing", async () => {
    setClipboard(undefined);
    renderButton();

    fireEvent.click(screen.getByRole("button", { name: "Copy address" }));
    await flush();

    expect(screen.getByRole("status")).toHaveTextContent(failedText);
  });

  it("clears the failure text on the next successful attempt", async () => {
    const writeText = vi
      .fn()
      .mockRejectedValueOnce(new DOMException("denied", "NotAllowedError"))
      .mockResolvedValue(undefined);
    setClipboard({ writeText });
    renderButton();

    fireEvent.click(screen.getByRole("button", { name: "Copy address" }));
    await flush();
    expect(screen.getByRole("status")).toHaveTextContent(failedText);

    fireEvent.click(screen.getByRole("button", { name: "Copy address" }));
    await flush();
    expect(screen.getByRole("status")).not.toHaveTextContent(failedText);
    expect(screen.getByRole("status")).toHaveTextContent("Server address copied.");
  });

  it("copies nothing on focus, hover or render", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    setClipboard({ writeText });
    renderButton();

    const button = screen.getByRole("button", { name: "Copy address" });
    act(() => button.focus());
    fireEvent.mouseOver(button);
    await flush();

    expect(writeText).not.toHaveBeenCalled();
  });

  it("does not update state or leave a timer after unmount", async () => {
    setClipboard({ writeText: vi.fn().mockResolvedValue(undefined) });
    const { unmount } = renderButton();

    fireEvent.click(screen.getByRole("button", { name: "Copy address" }));
    await flush();
    unmount();

    expect(vi.getTimerCount()).toBe(0);
  });
});
