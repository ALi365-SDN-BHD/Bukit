#!/usr/bin/env python3
"""Check the actual CI selection/command AST without running workflow subprocesses."""

import ast
from pathlib import Path
import re
import textwrap


def check(workflow):
    # shortcut: repository's current job indentation, not a YAML parser; update this owner check if workflow structure changes.
    def job(name):
        match = re.search(r"(?ms)^  " + re.escape(name) + r":\n(.*?)(?=^  [\w-]+:\n|\Z)", workflow)
        assert match, name
        return match.group(1)

    fast = job("fast-gate")
    required = {
        "python3 -B scripts/checks/coverage/security-layer-self-test.py",
        "python3 -B scripts/checks/core-report-inventory.py --self-test",
        "python3 -B scripts/checks/test-infrastructure-platform-self-test.py",
    }
    assert required <= set(re.findall(r"^        run: (.+)$", fast, re.M))
    assert all(fast.index(command) < fast.index("- name: Restore Core") for command in required)
    step = re.search(r"(?ms)^      - name: Run non-instrumented platform specialties\n(.*?)(?=^      - |\Z)", job("platform-tests"))
    assert step and "        shell: python\n" in step.group(1)
    tree = ast.parse(textwrap.dedent(step.group(1).split("        run: |\n", 1)[1]))

    def assigns(node, name):
        return isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == name for target in node.targets)

    start = next(i for i, node in enumerate(tree.body) if assigns(node, "classes"))
    end = next(i for i, node in enumerate(tree.body) if assigns(node, "posix_process_methods"))
    assert start < end
    selection = ast.Module(body=tree.body[start:end], type_ignores=[])
    loops = [node for node in tree.body if isinstance(node, ast.For) and ast.unparse(node.iter) == "classes.items()"]
    assert len(loops) == 1
    loop = loops[0]
    assert ast.unparse(loop.target) == "(project, selected)"
    allowed = {id(node) for node in ast.walk(selection)} | {id(node) for node in ast.walk(loop.iter)}
    assert all(id(node) in allowed for node in ast.walk(tree) if isinstance(node, ast.Name) and node.id == "classes")
    commands = [node for node in ast.walk(tree) if assigns(node, "command")]
    assert len(commands) == 1 and commands[0] in list(ast.walk(loop))
    runs = [node for node in ast.walk(loop) if isinstance(node, ast.Call) and ast.unparse(node.func) == "subprocess.run"]
    assert len(runs) == 1 and [ast.unparse(arg) for arg in runs[0].args] == ["command"]
    expression = compile(ast.Expression(commands[0].value), "<platform-command>", "eval")
    for os_name in ("Linux", "Darwin", "Windows"):
        env = {"__builtins__": {}, "actual_os": os_name}
        exec(compile(selection, "<platform-selection>", "exec"), env)
        expected = {
            "Bukit.PluginHost.Tests": ["SystemProcessRunnerTests", "PluginPathValidatorTests", "PluginProtocolProbeTests"],
            "Bukit.Engine.Tests": ["ExternalToolProcessRunnerTests", "TestInfrastructureTests", "OutputDirectoryCleanerTests", "AssetPipelineTests", "TaxonomyRedirectWriterTests"],
            "Bukit.Cli.Tests": ["CleanCommandTests"],
        }
        if os_name != "Linux":
            expected["Bukit.Engine.Tests"] += ["SafeOutputFileSystemTests", "DirectoryCopyFollowSymlinksTests"]
            expected["Bukit.Shared.Tests"] = ["PlatformPathHelperTests", "PathUtilsTests", "PlatformSafeSourceFileOpenerTests"]
            expected["Bukit.Cli.Tests"] += ["PreviewCommandTests", "PreviewCommandExtendedTests", "DevCommandTests"]
        expected = {project: [project + "." + name for name in names] for project, names in expected.items()}
        assert env["classes"] == expected, (os_name, env["classes"])
        for project, selected in env["classes"].items():
            output = Path("TestResults/platform") / project
            actual = eval(expression, {"__builtins__": {}, "str": str, "project": project, "selected": selected, "output": output})
            assert actual == ["dotnet", "test", f"tests/{project}/{project}.csproj", "-c", "Release", "--filter",
                              "|".join("FullyQualifiedName~" + name for name in selected), "--logger", "trx;LogFileName=tests.trx", "--results-directory", str(output)]


if __name__ == "__main__":
    repo = Path(__file__).resolve().parents[2]
    workflow = (repo / ".github/workflows/ci.yaml").read_text(encoding="utf-8")
    check(workflow)
    for old, new in (("Bukit.Engine.Tests.TestInfrastructureTests", "Bukit.Engine.Tests.WrongTests"),
                     ("Bukit.PluginHost.Tests.PluginProtocolProbeTests", "Bukit.PluginHost.Tests.WrongTests"),
                     ("Bukit.Engine.Tests.TaxonomyRedirectWriterTests", "Bukit.Engine.Tests.WrongTests"),
                     ("Bukit.Cli.Tests.CleanCommandTests", "Bukit.Cli.Tests.WrongTests"),
                     ('"--filter"', '"--wrong-filter"'),
                     ("python3 -B scripts/checks/core-report-inventory.py --self-test", "true")):
        assert old in workflow
        try:
            check(workflow.replace(old, new))
        except AssertionError:
            pass
        else:
            raise AssertionError("Owner check accepted mutation: " + old)
    print("Infrastructure platform owner selfcheck: three OS selections, exact commands, helper order and six mutants passed")
