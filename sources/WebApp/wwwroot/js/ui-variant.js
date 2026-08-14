window.utfUiVariant = {
    getVariant: () => {
        const match = document.cookie.match(/(?:^|;\s*)utf_ui_variant=(classic|react)(?:;|$)/);
        return match?.[1] ?? null;
    },
    openReact: (configuredBasePath) => {
        const base = new URL(configuredBasePath || "/next", window.location.origin);
        base.pathname = base.pathname.replace(/\/$/, "");
        const secure = window.location.protocol === "https:" ? "; Secure" : "";
        document.cookie = `utf_ui_variant=react; Path=/; SameSite=Lax; Max-Age=15552000${secure}`;
        const path = window.location.pathname === "/" ? "/" : window.location.pathname;
        base.pathname = `${base.pathname}${path}`;
        base.search = window.location.search;
        base.hash = window.location.hash;
        window.location.assign(base.toString());
    }
};
