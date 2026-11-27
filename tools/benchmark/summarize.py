"""Summarize a BT vs FSM benchmark run into a Markdown table and charts.

Usage:
  python tools/benchmark/summarize.py <results.csv> --out <dir>
                                      [--scaling <scaling.csv>]
                                      [--static <static-metrics.csv>]

Reads the CSV that SimulationRunner writes (one row per simulated match) and produces,
in <dir>:
  summary.md                 per-matchup table, the per-side architecture comparison, and the
                             static-metrics table with --static
  win_rate.png               chaser win % by matchup
  capture_time_boxplot.png   distribution of last-capture times by matchup
  decide_ms.png              mean / max decide ms per team by matchup
  behaviour_changes.png      behaviour switches per minute per team by matchup
  scaling.png                with --scaling: BT nodes vs FSM transitions against N
  selection_cost.png         with --scaling: ns per selection against N, per authoring style

Charts need matplotlib; without it the tables are still written and the missing charts are
reported. Standard library otherwise.
"""

import argparse
import csv
import math
import os
import statistics
import sys

MATCHUP_ORDER = ["bt_bt", "fsm_fsm", "bt_fsm", "fsm_bt"]
MATCHUP_LABEL = {
    "bt_bt": "BT chaser\nBT runner",
    "fsm_fsm": "FSM chaser\nFSM runner",
    "bt_fsm": "BT chaser\nFSM runner",
    "fsm_bt": "FSM chaser\nBT runner",
}


def load_rows(path):
    with open(path, newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def num(row, key):
    """A float column, or None when the simulation left it empty (e.g. no capture)."""
    text = (row.get(key) or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def ordered_matchups(rows):
    present = {r["matchup"] for r in rows}
    known = [m for m in MATCHUP_ORDER if m in present]
    return known + sorted(present - set(known))


def group(rows):
    out = {}
    for row in rows:
        out.setdefault(row["matchup"], []).append(row)
    return out


def mean(values):
    values = [v for v in values if v is not None]
    return statistics.fmean(values) if values else None


def median(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


def fmt(value, digits=2):
    return "—" if value is None else f"{value:.{digits}f}"


# --- statistics -------------------------------------------------------------------------
#
# Proportions use Wilson score intervals, not the normal (Wald) approximation. Win rates here
# sit at or near 100%, where Wald collapses to "100.0 ± 0.0" — an interval of zero width, which
# claims certainty from 60 matches. Wilson stays inside [0, 1] and keeps a sane width at the
# boundary (60/60 -> about 94-100%).
def wilson(k, n, z=1.96):
    """95% Wilson score interval for k successes in n trials, as percentages."""
    if n == 0:
        return (None, None)
    p = k / n
    denom = 1.0 + z * z / n
    centre = (p + z * z / (2 * n)) / denom
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denom
    return (100.0 * max(0.0, centre - half), 100.0 * min(1.0, centre + half))


def mean_ci(values, z=1.96):
    """Mean and half-width of its 95% interval, from the standard error."""
    values = [v for v in values if v is not None]
    if not values:
        return (None, None, None)
    m = statistics.fmean(values)
    if len(values) < 2:
        return (m, None, 0.0)
    sd = statistics.stdev(values)
    return (m, z * sd / math.sqrt(len(values)), sd)


def fisher_exact_two_sided(a, b, c, d):
    """p for the 2x2 table [[a, b], [c, d]], summing tables no more likely than the observed one."""
    n = a + b + c + d
    if n == 0:
        return None
    row1, col1 = a + b, a + c

    def prob(x):
        return (math.comb(row1, x) * math.comb(n - row1, col1 - x)) / math.comb(n, col1)

    observed = prob(a)
    lo = max(0, col1 - (n - row1))
    hi = min(row1, col1)
    # 1e-9 of slack: tables of equal probability must count, and floats make equality brittle.
    return min(1.0, sum(prob(x) for x in range(lo, hi + 1) if prob(x) <= observed * (1 + 1e-9)))


def by_side(rows):
    """For each side, split the matches by that side's brain and compare the two.

    Two outcome columns on purpose. `last_capture` averages the last capture in every match
    that had one, including matches where the chasers never caught everybody — so a match that
    caught 2 of 4 and ran out of time contributes an early-looking time. `catch_all` averages
    only the matches that actually ended with every runner caught, which is what "time to catch
    them all" means.
    """
    out = []
    for side, column in (("chaser", "chaser_brain"), ("runner", "runner_brain")):
        for brain in ("bt", "fsm"):
            part = [r for r in rows if r[column] == brain]
            if not part:
                continue
            wins = sum(1 for r in part if r["winner"] == "ChasersWin")
            lo, hi = wilson(wins, len(part))
            caught_all = [r for r in part
                          if num(r, "runners_caught") is not None
                          and num(r, "runners_caught") == num(r, "runners_total")]
            last_m, last_ci, last_sd = mean_ci([num(r, "last_capture_s") for r in part])
            all_m, all_ci, all_sd = mean_ci([num(r, "last_capture_s") for r in caught_all])
            out.append({
                "side": side, "brain": brain, "n": len(part), "wins": wins,
                "win_pct": 100.0 * wins / len(part),
                "win_lo": lo, "win_hi": hi,
                "last_mean": last_m, "last_ci": last_ci, "last_sd": last_sd,
                "n_caught_all": len(caught_all),
                "all_mean": all_m, "all_ci": all_ci, "all_sd": all_sd,
            })
    return out


def side_tests(sides):
    """Fisher on win counts, and the gap between mean catch-all times, per side."""
    out = []
    for side in ("chaser", "runner"):
        pair = [s for s in sides if s["side"] == side]
        if len(pair) != 2:
            continue
        bt, fsm = pair[0], pair[1]
        p = fisher_exact_two_sided(bt["wins"], bt["n"] - bt["wins"],
                                   fsm["wins"], fsm["n"] - fsm["wins"])
        out.append({"side": side, "fisher_p": p,
                    "bt": bt, "fsm": fsm})
    return out


def summarize(rows, matchups, groups):
    """One stats dict per matchup, in display order."""
    table = []
    for matchup in matchups:
        part = groups[matchup]
        chaser_wins = sum(1 for r in part if r["winner"] == "ChasersWin")
        table.append({
            "matchup": matchup,
            "n": len(part),
            "chaser_win_pct": 100.0 * chaser_wins / len(part) if part else None,
            "mean_caught": mean([num(r, "runners_caught") for r in part]),
            "median_first_capture": median([num(r, "first_capture_s") for r in part]),
            "median_last_capture": median([num(r, "last_capture_s") for r in part]),
            "median_duration": median([num(r, "match_duration_s") for r in part]),
            "chaser_mean_decide": mean([num(r, "chaser_mean_decide_ms") for r in part]),
            "chaser_max_decide": max([num(r, "chaser_max_decide_ms") or 0.0 for r in part], default=None),
            "runner_mean_decide": mean([num(r, "runner_mean_decide_ms") for r in part]),
            "runner_max_decide": max([num(r, "runner_max_decide_ms") or 0.0 for r in part], default=None),
            "chaser_changes": mean([num(r, "chaser_changes_per_min") for r in part]),
            "runner_changes": mean([num(r, "runner_changes_per_min") for r in part]),
        })
    return table


def write_summary(table, out_dir, static_path, time_scales, charts_written, missing_charts,
                  sides=None, tests=None):
    lines = ["# BT vs FSM benchmark summary", ""]
    scales = ", ".join(f"{s:g}x" for s in sorted(time_scales))
    lines += [f"Matches: {sum(r['n'] for r in table)} · time scale: {scales or 'unknown'}", ""]

    lines += [
        "## Per matchup",
        "",
        "| matchup | n | chaser win % | mean caught | median first capture (s) | "
        "median last capture (s) | median duration (s) | chaser decide ms (mean/max) | "
        "runner decide ms (mean/max) | chaser switches/min | runner switches/min |",
        "| --- | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: |",
    ]
    for row in table:
        lines.append(
            f"| {row['matchup']} | {row['n']} | {fmt(row['chaser_win_pct'], 1)} | "
            f"{fmt(row['mean_caught'])} | {fmt(row['median_first_capture'])} | "
            f"{fmt(row['median_last_capture'])} | {fmt(row['median_duration'])} | "
            f"{fmt(row['chaser_mean_decide'], 4)} / {fmt(row['chaser_max_decide'], 4)} | "
            f"{fmt(row['runner_mean_decide'], 4)} / {fmt(row['runner_max_decide'], 4)} | "
            f"{fmt(row['chaser_changes'], 1)} | {fmt(row['runner_changes'], 1)} |")
    lines.append("")

    if sides:
        lines += [
            "## By architecture, per side",
            "",
            "Win % carries a 95% Wilson interval (Wald gives a zero-width interval at 100%). Two",
            "outcome columns: *last capture* averages every match that had a capture, *caught all*",
            "only the matches that ended with every runner caught — the second is what \"time to",
            "catch them all\" means.",
            "",
            "| side | brain | n | chaser win % (95% CI) | last capture (s) | caught all: n | caught all (s) |",
            "| --- | --- | --: | --: | --: | --: | --: |",
        ]
        for r in sides:
            ci = "—" if r["win_lo"] is None else f"{fmt(r['win_lo'], 1)}–{fmt(r['win_hi'], 1)}"
            last = fmt(r["last_mean"]) + ("" if r["last_ci"] is None else f" ± {fmt(r['last_ci'])}")
            allt = fmt(r["all_mean"]) + ("" if r["all_ci"] is None else f" ± {fmt(r['all_ci'])}")
            lines.append(
                f"| {r['side']} | {r['brain'].upper()} | {r['n']} | {fmt(r['win_pct'], 1)} ({ci}) | "
                f"{last} | {r['n_caught_all']} | {allt} |")
        lines.append("")

        if tests:
            lines += ["Win-count difference, Fisher's exact test (two-sided):", ""]
            for t in tests:
                verdict = "—"
                if t["fisher_p"] is not None:
                    verdict = ("not significant" if t["fisher_p"] >= 0.05 else "significant")
                    verdict = f"p = {t['fisher_p']:.3f} ({verdict} at 0.05)"
                lines.append(
                    f"- {t['side']}: BT {t['bt']['wins']}/{t['bt']['n']} vs "
                    f"FSM {t['fsm']['wins']}/{t['fsm']['n']} — {verdict}")
            lines += [
                "",
                "A win rate this close to the ceiling leaves the design almost no power to detect a",
                "difference, so a non-significant result here is weak evidence either way. The",
                "catch-all time is the column with real spread, and therefore the one to read.",
                "",
            ]

    if static_path:
        lines += ["## Static metrics", ""]
        static_rows = load_rows(static_path)
        if static_rows:
            headers = list(static_rows[0].keys())
            lines.append("| " + " | ".join(headers) + " |")
            lines.append("| " + " | ".join("---" for _ in headers) + " |")
            for row in static_rows:
                lines.append("| " + " | ".join((row.get(h) or "").strip() or "—" for h in headers) + " |")
        lines.append("")

    if charts_written:
        lines += ["## Charts", ""] + [f"- `{name}`" for name in charts_written] + [""]
    if missing_charts:
        lines += ["Charts not produced (matplotlib unavailable): " + ", ".join(missing_charts), ""]

    path = os.path.join(out_dir, "summary.md")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines))
    return path


def make_charts(table, rows, groups, matchups, out_dir, scaling_path):
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        names = ["win_rate.png", "capture_time_boxplot.png", "decide_ms.png", "behaviour_changes.png"]
        if scaling_path:
            names += ["scaling.png", "selection_cost.png"]
        return [], names

    written = []
    labels = [MATCHUP_LABEL.get(m, m) for m in matchups]

    def save(fig, name):
        fig.tight_layout()
        fig.savefig(os.path.join(out_dir, name), dpi=140)
        plt.close(fig)
        written.append(name)

    fig, ax = plt.subplots(figsize=(7, 4))
    ax.bar(labels, [r["chaser_win_pct"] or 0 for r in table], color="#4878a8")
    ax.axhline(50, color="#aa3939", linestyle="--", linewidth=1, label="even")
    ax.set_ylabel("chaser win %")
    ax.set_ylim(0, 100)
    ax.set_title("Chaser win rate by matchup")
    ax.legend()
    save(fig, "win_rate.png")

    data = [[v for v in (num(r, "last_capture_s") for r in groups[m]) if v is not None] for m in matchups]
    if any(data):
        fig, ax = plt.subplots(figsize=(7, 4))
        try:
            ax.boxplot(data, tick_labels=labels)  # matplotlib >= 3.9
        except TypeError:
            ax.boxplot(data, labels=labels)      # matplotlib < 3.9
        ax.set_ylabel("last capture (match seconds)")
        ax.set_title("When the final runner was caught")
        save(fig, "capture_time_boxplot.png")

    width = 0.2
    positions = range(len(matchups))
    fig, ax = plt.subplots(figsize=(8, 4))
    ax.bar([p - 1.5 * width for p in positions], [r["chaser_mean_decide"] or 0 for r in table], width, label="chaser mean")
    ax.bar([p - 0.5 * width for p in positions], [r["chaser_max_decide"] or 0 for r in table], width, label="chaser max")
    ax.bar([p + 0.5 * width for p in positions], [r["runner_mean_decide"] or 0 for r in table], width, label="runner mean")
    ax.bar([p + 1.5 * width for p in positions], [r["runner_max_decide"] or 0 for r in table], width, label="runner max")
    ax.set_xticks(list(positions))
    ax.set_xticklabels(labels)
    ax.set_ylabel("ms per decision tick")
    ax.set_title("Decision tick cost (includes shared action work)")
    ax.legend()
    save(fig, "decide_ms.png")

    fig, ax = plt.subplots(figsize=(7, 4))
    ax.bar([p - width / 2 for p in positions], [r["chaser_changes"] or 0 for r in table], width, label="chaser")
    ax.bar([p + width / 2 for p in positions], [r["runner_changes"] or 0 for r in table], width, label="runner")
    ax.set_xticks(list(positions))
    ax.set_xticklabels(labels)
    ax.set_ylabel("behaviour switches per minute")
    ax.set_title("Behaviour switching rate")
    ax.legend()
    save(fig, "behaviour_changes.png")

    if scaling_path:
        srows = load_rows(scaling_path)
        ns = [int(r["n"]) for r in srows]

        fig, ax = plt.subplots(figsize=(7, 4.5))
        ax.plot(ns, [int(r["bt_nodes"]) for r in srows], "o-", label="BT nodes")
        ax.plot(ns, [int(r["fsm_pure_transitions"]) for r in srows], "s-", label="FSM transitions (pure)")
        ax.plot(ns, [int(r["fsm_anystate_transitions"]) for r in srows], "^-", label="FSM transitions (any-state)")
        ax.set_xlabel("number of behaviours (N)")
        ax.set_ylabel("count")
        ax.set_title("Structure size against behaviour count")
        ax.legend()
        ax.grid(alpha=0.3)
        save(fig, "scaling.png")

        if "bt_ns_per_tick" in srows[0]:
            fig, ax = plt.subplots(figsize=(7, 4.5))
            ax.plot(ns, [float(r["bt_ns_per_tick"]) for r in srows], "o-", label="BT")
            ax.plot(ns, [float(r["fsm_pure_ns_per_tick"]) for r in srows], "s-", label="FSM (pure)")
            ax.plot(ns, [float(r["fsm_any_ns_per_tick"]) for r in srows], "^-", label="FSM (any-state)")
            if "fsm_ordered_ns_per_tick" in srows[0]:
                ax.plot(ns, [float(r["fsm_ordered_ns_per_tick"]) for r in srows], "d--",
                        label="FSM (order-leaning table)")
            ax.set_xlabel("number of behaviours (N)")
            ax.set_ylabel("ns per selection")
            ax.set_title("Selection cost by authoring style")
            ax.legend()
            ax.grid(alpha=0.3)
            save(fig, "selection_cost.png")

    return written, []


def main():
    parser = argparse.ArgumentParser(description="Summarize a BT vs FSM benchmark run.")
    parser.add_argument("results")
    parser.add_argument("--out", required=True)
    parser.add_argument("--scaling")
    parser.add_argument("--static")
    args = parser.parse_args()

    rows = load_rows(args.results)
    if not rows:
        sys.exit(f"no rows in {args.results}")

    os.makedirs(args.out, exist_ok=True)
    matchups = ordered_matchups(rows)
    groups = group(rows)
    table = summarize(rows, matchups, groups)
    time_scales = {num(r, "time_scale") for r in rows} - {None}

    written, missing = make_charts(table, rows, groups, matchups, args.out, args.scaling)
    sides = by_side(rows)
    tests = side_tests(sides)
    summary_path = write_summary(table, args.out, args.static, time_scales, written, missing,
                                 sides, tests)

    print(f"wrote {summary_path}")
    for name in written:
        print(f"wrote {os.path.join(args.out, name)}")
    if missing:
        print("matplotlib not installed; no charts: " + ", ".join(missing), file=sys.stderr)


if __name__ == "__main__":
    main()
