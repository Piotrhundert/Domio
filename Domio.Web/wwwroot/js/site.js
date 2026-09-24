// Domio – wspólne zachowania interfejsu.
// UI-02: prawdziwe zakładki/foldery dla ekranów wielosekcyjnych.

(() => {
    "use strict";

    const labelAliases = new Map([
        ["Moja aktualna składka na budżet domu", "Składka do domu"],
        ["Moje konta", "Konta"],
        ["Reguły cykliczne", "Cykliczne"],
        ["Planowane wystąpienia", "Plan"],
        ["Ostatnie operacje", "Operacje"],
        ["Konta gospodarstwa", "Konta"],
        ["Ostatnie księgowania", "Księgowania"],
        ["Terminy i zaległości", "Terminy"],
        ["Role objęte konfiguracją", "Role"],
        ["Domownicy gospodarstwa", "Domownicy"],
        ["Reguły składek", "Reguły"],
        ["Miesięczne zobowiązania", "Zobowiązania"],
        ["Wpłaty do akceptacji", "Wpłaty"],
        ["Moje wysłane wpłaty", "Wpłaty"],
        ["Aktywne reguły składek", "Reguły"],
        ["Dzieci bez konta użytkownika", "Dzieci"],
        ["Kafelki kategorii faktur", "Kategorie"],
        ["Historia płatności faktur", "Historia płatności"],
        ["Zobowiązania", "Zobowiązania"],
        ["Wpłaty do akceptacji i historia", "Wpłaty"],
        ["Konta użytkowników", "Konta"],
        ["Osoby bez konta logowania", "Osoby bez konta"],
        ["Role w Domio", "Role"]
    ]);

    const normalizeLabel = (heading) => {
        const raw = (heading?.textContent ?? "")
            .replace(/\s+/g, " ")
            .trim();

        if (!raw) {
            return "Sekcja";
        }

        if (raw.startsWith("Rejestr faktur")) {
            return "Faktury";
        }

        return labelAliases.get(raw) ?? raw;
    };

    const slugify = (value) =>
        value
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, "-")
            .replace(/^-+|-+$/g, "") || "sekcja";

    const safeSessionGet = (key) => {
        try {
            return window.sessionStorage.getItem(key);
        } catch {
            return null;
        }
    };

    const safeSessionSet = (key, value) => {
        try {
            window.sessionStorage.setItem(key, value);
        } catch {
            // Brak dostępu do sessionStorage nie powinien blokować interfejsu.
        }
    };

    const buildTabs = ({
        page,
        panels,
        insertBefore = null,
        insertAfter = null,
        groupKey,
        headingSelector = "h2"
    }) => {
        const panelList = Array.from(panels)
            .filter(panel => panel instanceof HTMLElement);

        if (!page || panelList.length < 2) {
            return;
        }

        // Nie budujemy drugiego zestawu dla tego samego ekranu.
        if (page.querySelector(`.domio-folder-tabs[data-domio-tab-group="${groupKey}"]`)) {
            return;
        }

        const items = panelList.map((panel, index) => {
            const heading = panel.querySelector(headingSelector);
            const label = normalizeLabel(heading);
            const baseKey = slugify(label);
            const key = `${baseKey}-${index + 1}`;

            panel.classList.add("domio-tab-panel");
            panel.dataset.domioTabPanel = key;
            panel.setAttribute("role", "tabpanel");

            return { panel, label, key };
        });

        const storageKey = `domio.tabs.${window.location.pathname}.${groupKey}`;
        const savedKey = safeSessionGet(storageKey);
        const selectedItem = items.find(x => x.key === savedKey) ?? items[0];

        const nav = document.createElement("nav");
        nav.className = "domio-folder-tabs";
        nav.dataset.domioTabGroup = groupKey;
        nav.setAttribute("role", "tablist");
        nav.setAttribute("aria-label", "Zakładki modułu");

        const activate = (key, focus = false) => {
            for (const item of items) {
                const active = item.key === key;
                item.panel.hidden = !active;
                item.panel.classList.toggle("is-active", active);
                item.panel.setAttribute("aria-hidden", active ? "false" : "true");
            }

            for (const button of nav.querySelectorAll(".domio-folder-tab")) {
                const active = button.dataset.domioTabTarget === key;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-selected", active ? "true" : "false");
                button.tabIndex = active ? 0 : -1;

                if (active && focus) {
                    button.focus();
                }
            }

            safeSessionSet(storageKey, key);
        };

        items.forEach((item, index) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "domio-folder-tab";
            button.textContent = item.label;
            button.dataset.domioTabTarget = item.key;
            button.setAttribute("role", "tab");
            button.setAttribute("aria-selected", "false");
            button.tabIndex = -1;

            button.addEventListener("click", () => activate(item.key));

            button.addEventListener("keydown", (event) => {
                if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
                    return;
                }

                event.preventDefault();
                const delta = event.key === "ArrowRight" ? 1 : -1;
                const nextIndex = (index + delta + items.length) % items.length;
                activate(items[nextIndex].key, true);
            });

            nav.appendChild(button);
        });

        if (insertBefore?.parentNode) {
            insertBefore.parentNode.insertBefore(nav, insertBefore);
        } else if (insertAfter?.parentNode) {
            insertAfter.insertAdjacentElement("afterend", nav);
        } else {
            page.prepend(nav);
        }

        page.classList.add("domio-tabs-enhanced");
        activate(selectedItem.key);
    };

    const initPersonalFinanceTabs = () => {
        const page = document.querySelector(".finance-page");
        if (!page || page.closest(".family-finance-page")) {
            return;
        }

        const panels = page.querySelectorAll(":scope > section.finance-section");
        const header = page.querySelector(":scope > .finance-page-header");

        buildTabs({
            page,
            panels,
            insertAfter: header,
            groupKey: "personal-finance"
        });
    };

    const initHouseholdFinanceTabs = () => {
        const pages = document.querySelectorAll(".household-finance-page");

        for (const page of pages) {
            const panels = page.querySelectorAll(":scope > section.household-finance-section");
            const header = page.querySelector(":scope > .household-finance-header");

            buildTabs({
                page,
                panels,
                insertAfter: header,
                groupKey: "household-finance"
            });
        }
    };

    const initUsersTabs = () => {
        const page = document.querySelector(".users-page:not(.roles-single-page):not(.profile-page)");
        if (!page) {
            return;
        }

        const panels = page.querySelectorAll(":scope > section.users-panel");
        const header = page.querySelector(":scope > .users-page-header");

        buildTabs({
            page,
            panels,
            insertAfter: header,
            groupKey: "users"
        });
    };

    const initProfileTabs = () => {
        const page = document.querySelector(".profile-page");
        if (!page) {
            return;
        }

        const panels = page.querySelectorAll(".profile-card");
        const hero = page.querySelector(":scope > .profile-hero-card");
        const grid = page.querySelector(":scope > .profile-section-grid");

        buildTabs({
            page,
            panels,
            insertAfter: hero ?? page.querySelector(":scope > .users-page-header"),
            insertBefore: hero ? null : grid,
            groupKey: "profile"
        });
    };

    const initRolePermissionTabs = () => {
        const page = document.querySelector(".roles-single-page");
        if (!page) {
            return;
        }

        const body = page.querySelector(".role-permission-body");
        const panels = body?.querySelectorAll(":scope > .permission-module-group") ?? [];
        const firstPanel = panels.length > 0 ? panels[0] : null;

        buildTabs({
            page,
            panels,
            insertBefore: firstPanel,
            groupKey: "role-permissions",
            headingSelector: ".permission-module-heading strong"
        });
    };

    const enhanceExistingFamilyTabs = () => {
        const nav = document.querySelector(".family-finance-folder-tabs");
        if (!nav) {
            return;
        }

        nav.setAttribute("role", "tablist");
        nav.setAttribute("aria-label", "Zakładki finansów rodzinnych");

        for (const tab of nav.querySelectorAll(".family-finance-folder-tab")) {
            tab.setAttribute("role", "tab");
            tab.setAttribute(
                "aria-selected",
                tab.classList.contains("is-active") ? "true" : "false");
        }
    };

    document.addEventListener("DOMContentLoaded", () => {
        enhanceExistingFamilyTabs();
        initPersonalFinanceTabs();
        initHouseholdFinanceTabs();
        initUsersTabs();
        initProfileTabs();
        initRolePermissionTabs();
    });
})();
