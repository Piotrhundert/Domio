﻿// Domio – wspólne zachowania interfejsu.
// UI-03: uproszczone zakładki główne + zakładki wewnętrzne dla rozbudowanych ekranów.

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
        ["Role w Domio", "Role"],
        ["Dane osobowe", "Dane osobowe"],
        ["Kontakt", "Kontakt"],
        ["Dokument tożsamości", "Dokument"],
        ["Adres korespondencyjny", "Adres"],
        ["Osoba kontaktowa", "Osoba kontaktowa"],
        ["Konto i bezpieczeństwo", "Bezpieczeństwo"],
        ["Notatki i historia profilu", "Historia"]
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
            // Brak sessionStorage nie powinien blokować interfejsu.
        }
    };

    const appendExternalTab = (nav, label, href, key, before = null) => {
        if (!nav || nav.querySelector(`[data-domio-external-tab="${key}"]`)) {
            return;
        }

        const link = document.createElement("a");
        link.className = "domio-folder-tab";
        link.textContent = label;
        link.href = href;
        link.dataset.domioExternalTab = key;
        link.setAttribute("role", "tab");
        link.setAttribute("aria-selected", "false");

        if (before && before.parentNode === nav) {
            nav.insertBefore(link, before);
        } else {
            nav.appendChild(link);
        }
    };

    const isControllerIndexPath = (controllerName) => {
        const path = window.location.pathname
            .toLowerCase()
            .replace(/\/+$/, "");
        const controller = controllerName.toLowerCase();

        return path.endsWith(`/${controller}`) ||
            path.endsWith(`/${controller}/index`);
    };

    const buildGroupedTabs = ({
        page,
        panels,
        insertBefore = null,
        insertAfter = null,
        groupKey,
        headingSelector = "h2",
        groups = []
    }) => {
        const panelList = Array.from(panels)
            .filter(panel => panel instanceof HTMLElement);

        if (!page || panelList.length < 2) {
            return;
        }

        if (page.querySelector(`.domio-folder-tabs[data-domio-tab-group="${groupKey}"]`)) {
            return;
        }

        const panelItems = panelList.map((panel, index) => {
            const heading = panel.querySelector(headingSelector);
            const label = normalizeLabel(heading);

            return {
                panel,
                label,
                index
            };
        });

        const assigned = new Set();
        const groupedItems = [];

        for (const definition of groups) {
            const matches = panelItems.filter(item =>
                !assigned.has(item.index) &&
                definition.labels.includes(item.label));

            if (matches.length === 0) {
                continue;
            }

            matches.forEach(item => assigned.add(item.index));
            groupedItems.push({
                label: definition.label,
                items: matches
            });
        }

        for (const item of panelItems) {
            if (assigned.has(item.index)) {
                continue;
            }

            groupedItems.push({
                label: item.label,
                items: [item]
            });
        }

        if (groupedItems.length < 2) {
            return;
        }

        const commonParent = panelList[0].parentNode;
        if (!commonParent) {
            return;
        }

        const groupViews = groupedItems.map((group, groupIndex) => {
            const groupKeyValue = `${slugify(group.label)}-${groupIndex + 1}`;
            const wrapper = document.createElement("div");
            wrapper.className = "domio-tab-group-panel";
            wrapper.dataset.domioTabGroupPanel = groupKeyValue;
            wrapper.setAttribute("role", "tabpanel");

            const firstPanel = group.items[0].panel;
            commonParent.insertBefore(wrapper, firstPanel);

            let subnav = null;
            let subItems = [];

            if (group.items.length > 1) {
                subnav = document.createElement("nav");
                subnav.className = "domio-subtabs";
                subnav.setAttribute("role", "tablist");
                subnav.setAttribute("aria-label", `${group.label} – szczegóły`);
                wrapper.appendChild(subnav);
            }

            group.items.forEach((item, itemIndex) => {
                const subKey = `${slugify(item.label)}-${itemIndex + 1}`;
                item.panel.classList.add("domio-subtab-panel");
                item.panel.dataset.domioSubtabPanel = subKey;
                item.panel.removeAttribute("hidden");
                wrapper.appendChild(item.panel);

                if (subnav) {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "domio-subtab";
                    button.textContent = item.label;
                    button.dataset.domioSubtabTarget = subKey;
                    button.setAttribute("role", "tab");
                    button.setAttribute("aria-selected", "false");
                    subnav.appendChild(button);

                    subItems.push({
                        key: subKey,
                        panel: item.panel,
                        button
                    });
                }
            });

            if (subnav && subItems.length > 0) {
                const subStorageKey =
                    `domio.subtabs.${window.location.pathname}.${groupKey}.${groupKeyValue}`;
                const storedSub = safeSessionGet(subStorageKey);
                const initialSub =
                    subItems.find(x => x.key === storedSub) ?? subItems[0];

                const activateSub = (key) => {
                    for (const subItem of subItems) {
                        const active = subItem.key === key;
                        subItem.panel.hidden = !active;
                        subItem.button.classList.toggle("is-active", active);
                        subItem.button.setAttribute(
                            "aria-selected",
                            active ? "true" : "false");
                    }

                    safeSessionSet(subStorageKey, key);
                };

                for (const subItem of subItems) {
                    subItem.button.addEventListener(
                        "click",
                        () => activateSub(subItem.key));
                }

                activateSub(initialSub.key);
            }

            return {
                label: group.label,
                key: groupKeyValue,
                wrapper
            };
        });

        const storageKey =
            `domio.tabs.${window.location.pathname}.${groupKey}`;
        const savedKey = safeSessionGet(storageKey);
        const selected =
            groupViews.find(x => x.key === savedKey) ?? groupViews[0];

        const nav = document.createElement("nav");
        nav.className = "domio-folder-tabs domio-primary-tabs";
        nav.dataset.domioTabGroup = groupKey;
        nav.setAttribute("role", "tablist");
        nav.setAttribute("aria-label", "Zakładki modułu");

        const activate = (key, focus = false) => {
            for (const item of groupViews) {
                const active = item.key === key;
                item.wrapper.hidden = !active;
                item.wrapper.classList.toggle("is-active", active);
                item.wrapper.setAttribute(
                    "aria-hidden",
                    active ? "false" : "true");
            }

            for (const button of nav.querySelectorAll(".domio-folder-tab")) {
                if (!(button instanceof HTMLButtonElement)) {
                    continue;
                }

                const active =
                    button.dataset.domioTabTarget === key;
                button.classList.toggle("is-active", active);
                button.setAttribute(
                    "aria-selected",
                    active ? "true" : "false");
                button.tabIndex = active ? 0 : -1;

                if (active && focus) {
                    button.focus();
                }
            }

            safeSessionSet(storageKey, key);
        };

        groupViews.forEach((item, index) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "domio-folder-tab";
            button.textContent = item.label;
            button.dataset.domioTabTarget = item.key;
            button.setAttribute("role", "tab");
            button.setAttribute("aria-selected", "false");
            button.tabIndex = -1;

            button.addEventListener(
                "click",
                () => activate(item.key));

            button.addEventListener("keydown", (event) => {
                if (event.key !== "ArrowLeft" &&
                    event.key !== "ArrowRight") {
                    return;
                }

                event.preventDefault();
                const delta =
                    event.key === "ArrowRight" ? 1 : -1;
                const nextIndex =
                    (index + delta + groupViews.length) %
                    groupViews.length;

                activate(groupViews[nextIndex].key, true);
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

        page.classList.add("domio-tabs-enhanced", "domio-tabs-ui03");
        activate(selected.key);
    };

    const getPanelLabels = (panels, headingSelector = "h2") =>
        Array.from(panels)
            .map(panel =>
                normalizeLabel(
                    panel.querySelector(headingSelector)));

    const initPersonalFinanceTabs = () => {
        const page = document.querySelector(".finance-page");
        if (!page || page.closest(".family-finance-page")) {
            return;
        }

        const panels =
            page.querySelectorAll(":scope > section.finance-section");
        const header =
            page.querySelector(":scope > .finance-page-header");

        buildGroupedTabs({
            page,
            panels,
            insertAfter: header,
            groupKey: "personal-finance",
            groups: [
                {
                    label: "Przegląd",
                    labels: ["Składka do domu", "Konta"]
                },
                {
                    label: "Plan",
                    labels: ["Cykliczne", "Plan"]
                },
                {
                    label: "Historia",
                    labels: ["Operacje"]
                }
            ]
        });

        if (isControllerIndexPath("PersonalFinance")) {
            appendExternalTab(
                page.querySelector(
                    '.domio-folder-tabs[data-domio-tab-group="personal-finance"]'),
                "Cele",
                "/FinancialGoals?scope=Personal",
                "financial-goals-personal");
        }
    };

    const initHouseholdFinanceTabs = () => {
        const pages =
            document.querySelectorAll(".household-finance-page");

        for (const page of pages) {
            const panels =
                page.querySelectorAll(
                    ":scope > section.household-finance-section");
            const header =
                page.querySelector(
                    ":scope > .household-finance-header");

            if (panels.length < 2) {
                continue;
            }

            const labels = getPanelLabels(panels);
            let groups = [];

            if (labels.includes("Terminy") &&
                labels.includes("Reguły")) {
                groups = [
                    {
                        label: "Przegląd",
                        labels: ["Terminy"]
                    },
                    {
                        label: "Ustawienia",
                        labels: ["Role", "Domownicy", "Reguły"]
                    },
                    {
                        label: "Rozliczenia",
                        labels: ["Zobowiązania", "Wpłaty"]
                    }
                ];
            } else if (labels.includes("Kategorie") &&
                       labels.includes("Faktury")) {
                groups = [
                    {
                        label: "Faktury",
                        labels: ["Faktury", "Historia płatności"]
                    },
                    {
                        label: "Kategorie",
                        labels: ["Kategorie"]
                    }
                ];
            } else if (isControllerIndexPath("HouseholdFinance") &&
                       labels.includes("Konta")) {
                groups = [
                    {
                        label: "Przegląd",
                        labels: ["Konta"]
                    },
                    {
                        label: "Historia",
                        labels: ["Księgowania"]
                    }
                ];
            }

            buildGroupedTabs({
                page,
                panels,
                insertAfter: header,
                groupKey:
                    isControllerIndexPath("HouseholdFinance")
                        ? "household-finance"
                        : `household-finance-${slugify(document.title)}`,
                groups
            });

            if (isControllerIndexPath("HouseholdFinance")) {
                appendExternalTab(
                    page.querySelector(
                        '.domio-folder-tabs[data-domio-tab-group="household-finance"]'),
                    "Cele",
                    "/FinancialGoals?scope=Household",
                    "financial-goals-household");
            }
        }
    };

    const initUsersTabs = () => {
        const page =
            document.querySelector(
                ".users-page:not(.roles-single-page):not(.profile-page)");

        if (!page) {
            return;
        }

        const panels =
            page.querySelectorAll(":scope > section.users-panel");
        const header =
            page.querySelector(":scope > .users-page-header");

        buildGroupedTabs({
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

        buildGroupedTabs({
            page,
            panels,
            insertAfter:
                hero ??
                page.querySelector(":scope > .users-page-header"),
            insertBefore: hero ? null : grid,
            groupKey: "profile",
            groups: [
                {
                    label: "Dane",
                    labels: [
                        "Dane osobowe",
                        "Kontakt",
                        "Dokument",
                        "Adres"
                    ]
                },
                {
                    label: "Bliscy",
                    labels: ["Osoba kontaktowa"]
                },
                {
                    label: "Bezpieczeństwo",
                    labels: ["Bezpieczeństwo"]
                },
                {
                    label: "Historia",
                    labels: ["Historia"]
                }
            ]
        });
    };

    const initRolePermissionTabs = () => {
        const page = document.querySelector(".roles-single-page");
        if (!page) {
            return;
        }

        const body = page.querySelector(".role-permission-body");
        const panels =
            body?.querySelectorAll(
                ":scope > .permission-module-group") ?? [];
        const firstPanel =
            panels.length > 0 ? panels[0] : null;

        buildGroupedTabs({
            page,
            panels,
            insertBefore: firstPanel,
            groupKey: "role-permissions",
            headingSelector: ".permission-module-heading strong"
        });
    };

    const queryValueFromHref = (element, name) => {
        if (!(element instanceof HTMLAnchorElement)) {
            return null;
        }

        try {
            return new URL(element.href).searchParams.get(name);
        } catch {
            return null;
        }
    };

    const enhanceExistingFamilyTabs = () => {
        const nav =
            document.querySelector(".family-finance-folder-tabs");

        if (!nav) {
            return;
        }

        nav.classList.add("domio-primary-tabs");
        nav.setAttribute("role", "tablist");
        nav.setAttribute(
            "aria-label",
            "Główne zakładki finansów rodzinnych");

        const links =
            Array.from(
                nav.querySelectorAll(
                    "a.family-finance-folder-tab"));

        const byTab = new Map();
        let familyGroupId = null;

        for (const link of links) {
            link.setAttribute("role", "tab");

            const tab = queryValueFromHref(link, "tab");
            if (tab) {
                byTab.set(tab, link);
            }

            if (!familyGroupId) {
                familyGroupId =
                    queryValueFromHref(link, "familyGroupId");
            }
        }

        const currentTab =
            new URL(window.location.href)
                .searchParams.get("tab") ?? "overview";

        const familyTabs =
            ["members", "children", "child-contributions"];

        const members = byTab.get("members");
        const children = byTab.get("children");
        const childContributions =
            byTab.get("child-contributions");
        const expenses = byTab.get("expenses");
        const shared = byTab.get("shared");

        if (members) {
            members.textContent = "Rodzina";
        }

        if (expenses) {
            expenses.textContent = "Budżet";
        }

        if (shared) {
            shared.textContent = "Konto";
        }

        for (const hiddenLink of [children, childContributions]) {
            if (hiddenLink) {
                hiddenLink.classList.add("domio-primary-tab-hidden");
                hiddenLink.setAttribute("aria-hidden", "true");
                hiddenLink.tabIndex = -1;
            }
        }

        if (familyTabs.includes(currentTab) && members) {
            for (const link of links) {
                link.classList.remove("is-active");
                link.setAttribute("aria-selected", "false");
            }

            members.classList.add("is-active");
            members.setAttribute("aria-selected", "true");
        } else {
            for (const link of links) {
                link.setAttribute(
                    "aria-selected",
                    link.classList.contains("is-active")
                        ? "true"
                        : "false");
            }
        }

        const areaLink =
            links.find(link =>
                link.href.includes("/FamilyAreas"));

        if (expenses && areaLink &&
            expenses.parentNode === nav &&
            areaLink.parentNode === nav) {
            nav.insertBefore(expenses, areaLink);
        }

        if (familyGroupId) {
            appendExternalTab(
                nav,
                "Cele",
                `/FinancialGoals?scope=Family&familyGroupId=${encodeURIComponent(familyGroupId)}`,
                "financial-goals-family",
                shared);
        }

        if (familyTabs.includes(currentTab) &&
            members &&
            (children || childContributions) &&
            !document.querySelector(".family-finance-subtabs")) {
            const subnav = document.createElement("nav");
            subnav.className =
                "domio-subtabs family-finance-subtabs";
            subnav.setAttribute("role", "tablist");
            subnav.setAttribute(
                "aria-label",
                "Rodzina – szczegóły");

            const subEntries = [
                [members, "Członkowie", "members"],
                [children, "Przychody dzieci", "children"],
                [childContributions, "Składki dzieci", "child-contributions"]
            ];

            for (const [source, label, tab] of subEntries) {
                if (!(source instanceof HTMLAnchorElement)) {
                    continue;
                }

                const link = source.cloneNode(false);
                link.className =
                    `domio-subtab ${currentTab === tab ? "is-active" : ""}`;
                link.textContent = label;
                link.removeAttribute("aria-hidden");
                link.removeAttribute("tabindex");
                link.setAttribute("role", "tab");
                link.setAttribute(
                    "aria-selected",
                    currentTab === tab ? "true" : "false");
                subnav.appendChild(link);
            }

            nav.insertAdjacentElement("afterend", subnav);
        }
    };

    const enhanceGoalModuleTabs = () => {
        const nav =
            document.querySelector(".financial-goal-module-tabs");

        if (!nav) {
            return;
        }

        const firstLink =
            nav.querySelector("a.domio-folder-tab");

        if (firstLink) {
            firstLink.textContent = "Przegląd";
        }

        const url = new URL(window.location.href);
        const scope = url.searchParams.get("scope");
        const familyGroupId =
            url.searchParams.get("familyGroupId");

        const hasFamilyAreasTab =
            Array.from(nav.querySelectorAll("a.domio-folder-tab"))
                .some(link => {
                    try {
                        return new URL(link.href)
                            .pathname
                            .toLowerCase()
                            .includes("/familyareas");
                    } catch {
                        return false;
                    }
                });

        if (scope === "Family" &&
            familyGroupId &&
            !hasFamilyAreasTab &&
            !nav.querySelector(
                '[data-domio-external-tab="family-areas"]')) {
            const activeGoal =
                nav.querySelector(".domio-folder-tab.is-active");

            appendExternalTab(
                nav,
                "Obszary",
                `/FamilyAreas?familyGroupId=${encodeURIComponent(familyGroupId)}`,
                "family-areas",
                activeGoal);
        }
    };

    document.addEventListener("DOMContentLoaded", () => {
        enhanceExistingFamilyTabs();
        enhanceGoalModuleTabs();
        initPersonalFinanceTabs();
        initHouseholdFinanceTabs();
        initUsersTabs();
        initProfileTabs();
        initRolePermissionTabs();
    });
})();
