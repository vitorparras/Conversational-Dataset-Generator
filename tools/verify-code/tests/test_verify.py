"""Tests for tools/verify-code/verify.py and the steps that run suites. Standard library only."""

import contextlib
import importlib.util
import io
import re
import sys
import tempfile
import unittest
from pathlib import Path

_HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("verify_code_support", _HERE / "_support.py")
support = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(support)

_main_spec = importlib.util.spec_from_file_location("verify_code_main", _HERE.parent / "verify.py")
verify_code = importlib.util.module_from_spec(_main_spec)
sys.modules["verify_code_main"] = verify_code  # dataclasses resolves annotations through sys.modules
_main_spec.loader.exec_module(verify_code)

from steps import build_core, build_service, test_core, test_service, test_tools  # noqa: E402
from steps.common import Context, StepResult, run_command  # noqa: E402
from steps.reports import FAILED, PASSED, SKIPPED, TestOutcome, read_junit, read_trx  # noqa: E402

REPOSITORY = _HERE.parents[2]
WORKFLOW = REPOSITORY / ".github" / "workflows" / "verify-code.yml"


def listed_steps() -> list[str]:
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        code = verify_code.main(["--list"])
    assert code == 0
    return buffer.getvalue().split()


def workflow_steps() -> list[str]:
    """The steps the workflow runs, in file order, read with a plain regex (no YAML library)."""
    text = WORKFLOW.read_text(encoding="utf-8")
    steps: list[str] = []
    for match in re.finditer(r"verify\.py(?:\s+--ci)?\s+--step\s+([a-z_,]+)", text):
        steps.extend(match.group(1).split(","))
    return steps


class OrderTests(unittest.TestCase):
    def test_list_follows_the_declared_order_and_every_step_has_a_module(self) -> None:
        names = listed_steps()
        self.assertEqual(names, list(verify_code.STEP_ORDER))
        for name in names:
            module = verify_code.load_step(name)
            self.assertEqual(module.NAME, name)
            self.assertTrue(module.DESCRIPTION)

    def test_the_workflow_runs_the_same_steps_in_the_same_order_as_list(self) -> None:
        """Issue 37 CA-4: same steps, same order, locally and in CI."""
        self.assertEqual(workflow_steps(), listed_steps())

    def test_the_workflow_does_not_repeat_a_floor_or_a_command_of_its_own(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertNotIn("dotnet test", text)
        self.assertNotIn("pytest", text)

    def test_unknown_step_exits_with_two(self) -> None:
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(verify_code.main(["--step", "nope"]), 2)


class SuiteStepTests(support.RepositoryTestCase):
    def test_a_service_directory_without_tests_fails(self) -> None:
        """Issue 37 CA-3: a step that finds nothing to run fails, it does not pass empty."""
        self.write("language-service/pyproject.toml", "[project]\n")
        result = test_service.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("language-service/tests", " ".join(result.lines))

    def test_a_missing_solution_fails_both_core_steps(self) -> None:
        for step in (build_core, test_core):
            self.assertFalse(step.run(self.context()).ok, step.NAME)

    def test_a_missing_service_project_fails_the_service_steps(self) -> None:
        self.assertFalse(build_service.run(self.context()).ok)
        self.assertFalse(test_tools.run(self.context()).ok)

    def test_zero_tests_executed_fails_in_every_suite(self) -> None:
        core, service, tools = StepResult(), StepResult(), StepResult()
        test_core.evaluate(self.context(), [], ["Core.Tests.Unit"], core)
        test_service.evaluate(self.context(), [TestOutcome("t", SKIPPED, "m")], service)
        test_tools.evaluate(self.context(), "Ran 0 tests in 0.0s", tools)
        self.assertFalse(core.ok)
        self.assertFalse(service.ok)
        self.assertFalse(tools.ok)

    def test_a_core_project_that_ran_nothing_fails_even_if_others_ran(self) -> None:
        result = StepResult()
        outcomes = [TestOutcome("a", PASSED, "Core.Tests.Unit.dll")]
        test_core.evaluate(self.context(), outcomes, ["Core.Tests.Unit", "Core.Tests.Privacy"], result)
        self.assertFalse(result.ok)
        self.assertIn("Core.Tests.Privacy", " ".join(result.lines))

    def test_a_failed_test_fails_the_step(self) -> None:
        result = StepResult()
        test_service.evaluate(self.context(), [TestOutcome("t", PASSED, "m"), TestOutcome("bad", FAILED, "m")], result)
        self.assertFalse(result.ok)

    def test_a_skip_is_a_warning_locally_and_a_failure_naming_the_test_in_ci(self) -> None:
        outcomes = [TestOutcome("ok", PASSED, "m"), TestOutcome("dumps", SKIPPED, "tests.unit.test_entry")]
        local, ci = StepResult(), StepResult()
        test_service.evaluate(self.context(ci=False), outcomes, local)
        test_service.evaluate(self.context(ci=True), outcomes, ci)
        self.assertTrue(local.ok)
        self.assertTrue(any("dumps" in w for w in local.warnings))
        self.assertFalse(ci.ok)
        self.assertIn("dumps", " ".join(ci.lines))

    def test_a_skip_in_the_core_suite_is_a_failure_in_ci_naming_the_test(self) -> None:
        outcomes = [TestOutcome("Real", PASSED, "Core.Tests.Unit.dll"), TestOutcome("NeedsUv", SKIPPED, "Core.Tests.Unit.dll")]
        result = StepResult()
        test_core.evaluate(self.context(ci=True), outcomes, ["Core.Tests.Unit"], result)
        self.assertFalse(result.ok)
        self.assertIn("NeedsUv", " ".join(result.lines))

    def test_a_command_that_is_not_installed_fails_with_the_name(self) -> None:
        result = StepResult()
        self.assertFalse(run_command(["zqx-no-such-tool-7731"], self.root, result))
        self.assertIn("zqx-no-such-tool-7731", " ".join(result.lines))


class ReportReaderTests(unittest.TestCase):
    def test_trx_outcomes_and_assemblies(self) -> None:
        outcomes = read_trx(support.FIXTURES / "results.trx")
        by_name = {o.name.rsplit(".", 1)[-1]: o for o in outcomes}
        self.assertEqual(by_name["Holds"], TestOutcome(by_name["Holds"].name, PASSED, "Core.Tests.Properties.dll"))
        self.assertEqual(by_name["SkippedOne"].outcome, SKIPPED)
        self.assertEqual(by_name["Fails"].outcome, FAILED)
        self.assertEqual(by_name["Fails"].source, "Core.Tests.Unit.dll")

    def test_junit_outcomes(self) -> None:
        by_name = {o.name: o.outcome for o in read_junit(support.FIXTURES / "junit.xml")}
        self.assertEqual(
            by_name,
            {"test_refuses": PASSED, "test_holds": PASSED, "test_dumps": SKIPPED, "test_broken": FAILED},
        )


class ReportTests(unittest.TestCase):
    def test_the_report_has_a_time_per_step_and_fails_when_one_failed(self) -> None:
        outcomes = [verify_code.Outcome("a", "passou", 1.5), verify_code.Outcome("b", "FALHOU", 2.0)]
        report = verify_code.render_report(outcomes)
        self.assertIn("| a | passou | 1.5 s |", report)
        self.assertIn("| total | FALHOU | 3.5 s |", report)

    def test_a_step_whose_input_failed_is_not_run(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            context = Context(root=Path(tmp), out_dir=Path(tmp) / "out")
            with contextlib.redirect_stdout(io.StringIO()):
                outcomes = verify_code.execute(["build_core", "test_core"], context, check_requirements=True)
        self.assertEqual([(o.name, o.status) for o in outcomes], [("build_core", "FALHOU"), ("test_core", "não rodou")])


if __name__ == "__main__":
    unittest.main()
