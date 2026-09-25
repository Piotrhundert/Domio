(() => {
    const parseJson = (id) => {
        const node = document.getElementById(id);
        if (!node) return null;
        try { return JSON.parse(node.textContent || "null"); } catch { return null; }
    };

    const css = (name, fallback) => getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
    const colors = {
        income: css("--bs-primary", "#0d6efd"),
        expense: css("--bs-danger", "#dc3545"),
        net: css("--bs-success", "#198754"),
        muted: "#728397",
        grid: "#e7edf3",
        baseline: "#6c757d",
        scenario: css("--bs-primary", "#0d6efd")
    };

    const money = (value) => new Intl.NumberFormat("pl-PL", { maximumFractionDigits: 0 }).format(value || 0);

    function prepareCanvas(canvas) {
        if (!canvas) return null;
        const rect = canvas.getBoundingClientRect();
        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(640, Math.floor(rect.width || 640));
        const height = Number(canvas.getAttribute("height")) || 260;
        canvas.width = width * dpr;
        canvas.height = height * dpr;
        const ctx = canvas.getContext("2d");
        ctx.scale(dpr, dpr);
        return { ctx, width, height };
    }

    function drawAxes(ctx, width, height, min, max, labels) {
        const pad = { l: 54, r: 18, t: 18, b: 42 };
        const plotW = width - pad.l - pad.r;
        const plotH = height - pad.t - pad.b;
        ctx.clearRect(0, 0, width, height);
        ctx.font = "11px system-ui";
        ctx.fillStyle = colors.muted;
        ctx.strokeStyle = colors.grid;
        ctx.lineWidth = 1;
        for (let i = 0; i <= 4; i++) {
            const y = pad.t + (plotH * i / 4);
            ctx.beginPath(); ctx.moveTo(pad.l, y); ctx.lineTo(width - pad.r, y); ctx.stroke();
            const value = max - (max - min) * i / 4;
            ctx.fillText(money(value), 4, y + 4);
        }
        const step = Math.max(1, Math.ceil(labels.length / 6));
        labels.forEach((label, index) => {
            if (index % step !== 0 && index !== labels.length - 1) return;
            const x = labels.length === 1 ? pad.l + plotW / 2 : pad.l + plotW * index / (labels.length - 1);
            ctx.save(); ctx.translate(x, height - 12); ctx.rotate(-0.35); ctx.fillText(label, 0, 0); ctx.restore();
        });
        return { pad, plotW, plotH };
    }

    function drawLines(canvas, labels, series) {
        const prepared = prepareCanvas(canvas);
        if (!prepared || !labels.length) return;
        const { ctx, width, height } = prepared;
        const values = series.flatMap(s => s.values).filter(Number.isFinite);
        let min = Math.min(0, ...values), max = Math.max(0, ...values);
        if (max === min) max = min + 1;
        const { pad, plotW, plotH } = drawAxes(ctx, width, height, min, max, labels);
        const xFor = (i) => labels.length === 1 ? pad.l + plotW / 2 : pad.l + plotW * i / (labels.length - 1);
        const yFor = (v) => pad.t + plotH - ((v - min) / (max - min)) * plotH;
        series.forEach(s => {
            ctx.strokeStyle = s.color; ctx.lineWidth = 2.2; ctx.beginPath();
            s.values.forEach((v, i) => { const x = xFor(i), y = yFor(v); i ? ctx.lineTo(x, y) : ctx.moveTo(x, y); });
            ctx.stroke();
            ctx.fillStyle = s.color;
            s.values.forEach((v, i) => { ctx.beginPath(); ctx.arc(xFor(i), yFor(v), 2.7, 0, Math.PI * 2); ctx.fill(); });
        });
    }

    function drawBars(canvas, labels, planned, actual) {
        const prepared = prepareCanvas(canvas);
        if (!prepared || !labels.length) return;
        const { ctx, width, height } = prepared;
        const max = Math.max(1, ...planned, ...actual);
        const min = 0;
        const { pad, plotW, plotH } = drawAxes(ctx, width, height, min, max, labels);
        const groupW = plotW / labels.length;
        const barW = Math.max(5, Math.min(18, groupW * .28));
        labels.forEach((_, i) => {
            const center = pad.l + groupW * (i + .5);
            const pH = planned[i] / max * plotH;
            const aH = actual[i] / max * plotH;
            ctx.fillStyle = "#9abfe8";
            ctx.fillRect(center - barW - 2, pad.t + plotH - pH, barW, pH);
            ctx.fillStyle = colors.expense;
            ctx.fillRect(center + 2, pad.t + plotH - aH, barW, aH);
        });
    }

    const monthly = parseJson("finance-report-monthly-data") || [];
    const simulation = parseJson("finance-report-simulation-data");

    const renderCharts = () => {
        drawLines(
            document.getElementById("finance-flow-chart"),
            monthly.map(x => x.Label),
            [
                { values: monthly.map(x => Number(x.Income)), color: colors.income },
                { values: monthly.map(x => Number(x.Expense)), color: colors.expense },
                { values: monthly.map(x => Number(x.Net)), color: colors.net }
            ]);
        drawBars(
            document.getElementById("finance-plan-chart"),
            monthly.map(x => x.Label),
            monthly.map(x => Number(x.PlannedExpense)),
            monthly.map(x => Number(x.ActualExpense)));

        if (Array.isArray(simulation)) {
            drawLines(
                document.getElementById("finance-simulation-chart"),
                simulation.map(x => x.Label),
                [
                    { values: simulation.map(x => Number(x.BaselineBalance)), color: colors.baseline },
                    { values: simulation.map(x => Number(x.ScenarioBalance)), color: colors.scenario }
                ]);
        }
    };

    renderCharts();

    const scope = document.getElementById("scope");
    const familyFilter = document.getElementById("family-filter");
    if (scope && familyFilter) {
        const refreshFamily = () => familyFilter.style.display = scope.value === "Family" ? "" : "none";
        scope.addEventListener("change", refreshFamily);
        refreshFamily();
    }

    const setValue = (name, value) => {
        const el = document.querySelector(`[name="${name}"]`);
        if (el) el.value = value;
    };
    document.querySelectorAll("[data-sim-preset]").forEach(button => {
        button.addEventListener("click", () => {
            const preset = button.getAttribute("data-sim-preset");
            if (preset === "oszczedny") {
                setValue("IncomeChangePercent", 0); setValue("ExpenseChangePercent", -10);
                setValue("ExtraMonthlyIncome", 0); setValue("ExtraMonthlyExpense", 0);
            } else if (preset === "drozej") {
                setValue("ExpenseChangePercent", 10);
            } else if (preset === "dochod") {
                setValue("IncomeChangePercent", 10);
            } else if (preset === "reset") {
                ["IncomeChangePercent","ExpenseChangePercent","ExtraMonthlyIncome","ExtraMonthlyExpense","OneTimeIncome","OneTimeExpense","AnnualIncomeGrowthPercent","AnnualExpenseInflationPercent"].forEach(x => setValue(x, 0));
                setValue("OneTimeMonth", 1);
            }
        });
    });

    let resizeTimer;
    window.addEventListener("resize", () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(renderCharts, 150);
    });
})();
