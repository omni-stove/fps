import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

import comments  # noqa: E402


class CommentsCase(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.root)

    def write(self, name, text):
        path = os.path.join(self.root, name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        return path

    def read(self, name):
        with open(os.path.join(self.root, name), encoding="utf-8", newline="") as f:
            return f.read()

    def extract(self, name):
        return comments.extract_file(os.path.join(self.root, name), self.root)

    def ids(self, name):
        return {entry["text"]: entry["id"] for entry in self.extract(name)}

    def apply(self, judgments):
        lines = [json.dumps(j, ensure_ascii=False) if isinstance(j, dict) else j for j in judgments]
        return comments.apply(lines, "j.jsonl", self.root)


class ExtractTests(CommentsCase):
    def test_doc_comment_lines_are_one_block_with_the_following_declaration(self):
        self.write(
            "A.cs",
            "namespace N\n"
            "{\n"
            "    /// <summary>\n"
            "    /// A.\n"
            "    /// </summary>\n"
            "    public sealed class A\n"
            "    {\n"
            "        public int B;\n"
            "    }\n"
            "}\n",
        )

        entries = self.extract("A.cs")

        self.assertEqual(len(entries), 1)
        entry = entries[0]
        self.assertRegex(entry["id"], r"^A\.cs:3:5#[0-9a-f]{8}$")
        self.assertEqual(entry["file"], "A.cs")
        self.assertEqual((entry["start"], entry["end"]), (3, 5))
        self.assertEqual(entry["kind"], "block")
        self.assertEqual(entry["text"], "/// <summary>\n/// A.\n/// </summary>")
        self.assertEqual(entry["next"], ["public sealed class A", "{", "public int B;"])
        self.assertNotIn("code", entry)

    def test_blank_line_splits_blocks_and_mixed_markers_join(self):
        self.write(
            "A.cs",
            "class A\n"
            "{\n"
            "    // first\n"
            "\n"
            "    /// <summary>x</summary>\n"
            "    // reason\n"
            "    void M() { }\n"
            "}\n",
        )

        entries = self.extract("A.cs")

        self.assertEqual([(e["start"], e["end"]) for e in entries], [(3, 3), (5, 6)])
        self.assertEqual(entries[0]["next"], ["void M() { }", "}"])
        self.assertEqual(entries[1]["text"], "/// <summary>x</summary>\n// reason")

    def test_trailing_comment_is_its_own_entry_with_the_code(self):
        self.write("A.cs", "class A\n{\n    int a = 1; // one\n    int b = 2;\n}\n")

        entries = self.extract("A.cs")

        self.assertEqual(len(entries), 1)
        entry = entries[0]
        self.assertRegex(entry["id"], r"^A\.cs:3:16#[0-9a-f]{8}$")
        self.assertEqual(entry["kind"], "inline")
        self.assertEqual((entry["start"], entry["end"]), (3, 3))
        self.assertEqual(entry["text"], "// one")
        self.assertEqual(entry["code"], "int a = 1;")
        self.assertEqual(entry["next"], ["int b = 2;", "}"])

    def test_block_comments_on_their_own_lines_and_inside_code(self):
        self.write(
            "A.cs",
            "/* head\n"
            "   more */\n"
            "class A\n"
            "{\n"
            "    void M() { Foo(/* x */ 1); }\n"
            "}\n",
        )

        entries = self.extract("A.cs")

        self.assertEqual(len(entries), 2)
        self.assertEqual(entries[0]["kind"], "block")
        self.assertEqual((entries[0]["start"], entries[0]["end"]), (1, 2))
        self.assertEqual(entries[0]["text"], "/* head\nmore */")
        self.assertEqual(entries[0]["next"], ["class A", "{", "void M() { Foo(/* x */ 1); }"])
        self.assertEqual(entries[1]["kind"], "inline")
        self.assertEqual(entries[1]["text"], "/* x */")
        self.assertEqual(entries[1]["code"], "void M() { Foo( 1); }")

    def test_slashes_inside_strings_and_chars_are_not_comments(self):
        self.write(
            "A.cs",
            "\n".join(
                [
                    "class A",
                    "{",
                    '    string u = "http://example.com/*x*/";',
                    r'    string v = @"C:\dir\// ""quoted"" /*";',
                    '    string w = $"{(b ? "//" : "/*")} {{//}}";',
                    "    char c = '/';",
                    "    char q = '\"';",
                    '    string r = """',
                    "        // raw",
                    '        """;',
                    '    string s = "end"; // real',
                    "}",
                    "",
                ]
            ),
        )

        entries = self.extract("A.cs")

        self.assertEqual([(e["start"], e["text"]) for e in entries], [(11, "// real")])

    def test_directives(self):
        self.write(
            "A.cs",
            "#region Players' // stuff\n"
            "#pragma warning disable CS0618 // obsolete on purpose\n"
            "#if UNITY_EDITOR // editor only\n"
            "class A { }\n"
            "#endif\n"
            "#endregion\n",
        )

        entries = self.extract("A.cs")

        self.assertEqual([(e["start"], e["kind"], e["text"]) for e in entries], [(3, "inline", "// editor only")])

    def test_tool_comments_are_not_extracted(self):
        self.write(
            "A.cs",
            "// <auto-generated>\n"
            "// </auto-generated>\n"
            "class A\n"
            "{\n"
            "    // ReSharper disable once UnusedMember.Local\n"
            "    // what\n"
            "    void M() { }\n"
            "}\n",
        )

        entries = self.extract("A.cs")

        self.assertEqual([(e["start"], e["end"], e["text"]) for e in entries], [(6, 6, "// what")])

    def test_directory_walk_skips_build_output_and_writes_jsonl(self):
        self.write("src/A.cs", "// a\nclass A { }\n")
        self.write("src/obj/B.cs", "// b\nclass B { }\n")
        self.write("src/notes.txt", "// c\n")
        out = io.StringIO()

        cwd = os.getcwd()
        os.chdir(self.root)
        try:
            comments.extract(["src"], out)
        finally:
            os.chdir(cwd)

        records = [json.loads(line) for line in out.getvalue().splitlines()]
        self.assertEqual([(r["file"], r["text"]) for r in records], [("src/A.cs", "// a")])

    def test_missing_path_is_a_usage_error(self):
        with self.assertRaises(comments.UsageError):
            list(comments.cs_files([os.path.join(self.root, "nope")]))


class ApplyTests(CommentsCase):
    def test_delete_does_not_leave_double_blank_lines_or_blank_lines_next_to_braces(self):
        self.write(
            "A.cs",
            "class A\n"
            "{\n"
            "    // after brace\n"
            "\n"
            "    int a;\n"
            "\n"
            "    // between\n"
            "\n"
            "    int b;\n"
            "\n"
            "    // before close\n"
            "}\n",
        )
        ids = self.ids("A.cs")

        counts, errors = self.apply([{"id": i, "action": "delete"} for i in ids.values()])

        self.assertEqual(errors, [])
        self.assertEqual(counts["delete"], 3)
        self.assertEqual(self.read("A.cs"), "class A\n{\n    int a;\n\n    int b;\n}\n")

    def test_rewrite_uses_the_original_indent_and_line_comments(self):
        self.write(
            "A.cs",
            "namespace N\n"
            "{\n"
            "    class A\n"
            "    {\n"
            "        /// <summary>\n"
            "        /// 何かをする。\n"
            "        /// </summary>\n"
            "        void M() { }\n"
            "    }\n"
            "}\n",
        )
        (ident,) = self.ids("A.cs").values()

        counts, errors = self.apply([{"id": ident, "action": "rewrite", "rewrite": "理由その 1。\n\n理由その 2。\n"}])

        self.assertEqual(errors, [])
        self.assertEqual(counts["rewrite"], 1)
        self.assertEqual(
            self.read("A.cs"),
            "namespace N\n"
            "{\n"
            "    class A\n"
            "    {\n"
            "        // 理由その 1。\n"
            "        //\n"
            "        // 理由その 2。\n"
            "        void M() { }\n"
            "    }\n"
            "}\n",
        )

    def test_rewrite_of_a_trailing_comment_goes_above_the_line(self):
        self.write("A.cs", "class A\n{\n    int a = 1; // 何か\n}\n")
        (ident,) = self.ids("A.cs").values()

        _, errors = self.apply([{"id": ident, "action": "rewrite", "rewrite": "理由"}])

        self.assertEqual(errors, [])
        self.assertEqual(self.read("A.cs"), "class A\n{\n    // 理由\n    int a = 1;\n}\n")

    def test_rewrite_after_a_multiline_string_goes_above_the_statement(self):
        self.write("A.cs", 'class A\n{\n    string s = @"a\nb"; // c\n}\n')
        (ident,) = self.ids("A.cs").values()

        _, errors = self.apply([{"id": ident, "action": "rewrite", "rewrite": "理由"}])

        self.assertEqual(errors, [])
        self.assertEqual(self.read("A.cs"), 'class A\n{\n    // 理由\n    string s = @"a\nb";\n}\n')

    def test_delete_inline_comments(self):
        self.write(
            "A.cs",
            "class A\n"
            "{\n"
            "    int a = 1; // x\n"
            "    void M() { Foo(/* y */ 1); }\n"
            "    int/* z */b;\n"
            "}\n",
        )
        ids = self.ids("A.cs")

        _, errors = self.apply([{"id": i, "action": "delete"} for i in ids.values()])

        self.assertEqual(errors, [])
        self.assertEqual(
            self.read("A.cs"),
            "class A\n{\n    int a = 1;\n    void M() { Foo(1); }\n    int b;\n}\n",
        )

    def test_keep_changes_nothing(self):
        text = "class A\n{\n    // 理由\n    int a;\n}\n"
        self.write("A.cs", text)
        (ident,) = self.ids("A.cs").values()

        counts, errors = self.apply([{"id": ident, "action": "keep"}])

        self.assertEqual(errors, [])
        self.assertEqual(counts, {"keep": 1, "delete": 0, "rewrite": 0})
        self.assertEqual(self.read("A.cs"), text)

    def test_changed_comment_is_an_error_and_the_others_still_apply(self):
        self.write("A.cs", "class A\n{\n    // one\n    int a;\n\n    // two\n    int b;\n}\n")
        ids = self.ids("A.cs")
        self.write("A.cs", "class A\n{\n    // one\n    int a;\n\n    // TWO\n    int b;\n}\n")

        counts, errors = self.apply([{"id": ids["// one"], "action": "delete"}, {"id": ids["// two"], "action": "delete"}])

        self.assertEqual(counts["delete"], 1)
        self.assertEqual(len(errors), 1)
        self.assertIn("j.jsonl:2: " + ids["// two"], errors[0])
        self.assertIn("differs from the extracted one", errors[0])
        self.assertEqual(self.read("A.cs"), "class A\n{\n    int a;\n\n    // TWO\n    int b;\n}\n")

    def test_invalid_judgments_are_errors(self):
        self.write("A.cs", "class A\n{\n    // one\n    int a;\n\n    // two\n    int b;\n\n    // three\n    int c;\n}\n")
        ids = self.ids("A.cs")

        counts, errors = self.apply(
            [
                "not json",
                {"action": "delete"},
                {"id": ids["// one"], "action": "remove"},
                {"id": ids["// one"], "action": "rewrite", "rewrite": "<see cref=\"B\"/> に合わせる"},
                {"id": ids["// two"], "action": "rewrite", "rewrite": "/// 理由"},
                {"id": ids["// three"], "action": "rewrite"},
                {"id": ids["// three"], "action": "keep"},
                {"id": ids["// three"], "action": "delete"},
                {"id": "Missing.cs:1:1#0123abcd", "action": "delete"},
            ]
        )

        self.assertEqual(counts, {"keep": 1, "delete": 0, "rewrite": 0})
        self.assertEqual(len(errors), 8)
        self.assertIn("j.jsonl:1: invalid JSON", errors[0])
        self.assertIn("j.jsonl:2: missing or malformed \"id\"", errors[1])
        self.assertIn("\"action\" must be keep, delete or rewrite", errors[2])
        self.assertIn("XML doc tags", errors[3])
        self.assertIn("without comment markers", errors[4])
        self.assertIn("rewrite needs the new text", errors[5])
        self.assertIn("j.jsonl:8: " + ids["// three"] + ": duplicate id", errors[6])
        self.assertIn("Missing.cs:1:1#0123abcd: no such .cs file", errors[7])
        self.assertEqual(
            self.read("A.cs"), "class A\n{\n    // one\n    int a;\n\n    // two\n    int b;\n\n    // three\n    int c;\n}\n"
        )

    def test_bom_and_crlf_are_kept(self):
        self.write("A.cs", "\ufeffclass A\r\n{\r\n    // x\r\n    int a; // y\r\n}\r\n")
        ids = self.ids("A.cs")

        _, errors = self.apply(
            [{"id": ids["// x"], "action": "delete"}, {"id": ids["// y"], "action": "rewrite", "rewrite": "理由"}]
        )

        self.assertEqual(errors, [])
        with open(os.path.join(self.root, "A.cs"), "rb") as f:
            data = f.read()
        self.assertEqual(data, "\ufeffclass A\r\n{\r\n    // 理由\r\n    int a;\r\n}\r\n".encode("utf-8"))


@unittest.skipUnless(shutil.which("git"), "git is required")
class VerifyTests(CommentsCase):
    ORIGINAL = (
        "class A\n"
        "{\n"
        "    /// <summary>a</summary>\n"
        "    int a = 1; // one\n"
        "\n"
        "    // b\n"
        "\n"
        '    string s = "x  y";\n'
        "}\n"
    )

    def setUp(self):
        super().setUp()
        self.git("init", "-q")
        self.git("symbolic-ref", "HEAD", "refs/heads/main")
        self.write("A.cs", self.ORIGINAL)
        self.write("notes.md", "notes\n")
        self.git("add", "-A")
        self.git("commit", "-q", "-m", "base")

    def git(self, *args):
        subprocess.run(
            ["git", "-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false"] + list(args),
            cwd=self.root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )

    def test_comment_and_blank_line_changes_pass(self):
        self.write(
            "A.cs",
            "class A\n"
            "{\n"
            "    // 理由\n"
            "    int  a = 1;\n"
            '    string s = "x  y";\n'
            "}\n",
        )
        self.write("notes.md", "changed\n")

        self.assertEqual(comments.verify("main", self.root), [])

    def test_code_change_is_reported_with_its_line(self):
        self.write("A.cs", self.ORIGINAL.replace("int a = 1;", "int a = 2;"))

        problems = comments.verify("main", self.root)

        self.assertEqual(problems, ["A.cs:4 (main): - int a = 1; // one", "A.cs:4: + int a = 2; // one"])

    def test_whitespace_inside_a_string_is_a_change(self):
        self.write("A.cs", self.ORIGINAL.replace('"x  y"', '"x y"'))

        problems = comments.verify("main", self.root)

        self.assertEqual(problems, ['A.cs:8 (main): - string s = "x  y";', 'A.cs:8: + string s = "x y";'])

    def test_new_untracked_file_with_code_is_reported(self):
        self.write("B.cs", "// only\nclass B { }\n")

        problems = comments.verify("main", self.root)

        self.assertEqual(problems, ["B.cs:2: + class B { }"])

    def test_committed_changes_on_a_branch_are_compared_with_the_branch_point(self):
        self.git("checkout", "-q", "-b", "work")
        self.write("A.cs", self.ORIGINAL.replace("    // b\n\n", ""))
        self.git("commit", "-q", "-am", "drop comment")
        self.git("checkout", "-q", "main")
        self.write("C.cs", "class C { }\n")
        self.git("add", "C.cs")
        self.git("commit", "-q", "-m", "main moves on")
        self.git("checkout", "-q", "work")

        self.assertEqual(comments.verify("main", self.root), [])

    def test_unknown_ref_is_a_usage_error(self):
        with self.assertRaises(comments.UsageError):
            comments.verify("no-such-ref", self.root)


if __name__ == "__main__":
    unittest.main()
