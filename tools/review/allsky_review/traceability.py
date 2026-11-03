"""Requirement tags for Python code, the counterpart of [Requirement] in the C# projects."""


def requirement(*ids):
    """Tags a class or function with the requirements it implements or tests, for example
    @requirement("REQ-62"). The C# coverage test finds these tags in the source files."""

    def tag(target):
        setattr(target, "__requirements__", tuple(ids))
        return target

    return tag
