import sqlite3


def find(cursor: sqlite3.Cursor, name: str, table: str, template: str) -> None:
    # ruleid: sql-concatenation-python
    cursor.execute("SELECT id FROM person WHERE name = '" + name + "'")
    # ruleid: sql-concatenation-python
    cursor.execute(f"SELECT id FROM person WHERE name = '{name}'")
    # ruleid: sql-concatenation-python
    cursor.execute(template % table)
    # ruleid: sql-concatenation-python
    cursor.execute(template.format(table))
    query = "SELECT id FROM person WHERE name = '" + name + "'"
    # ruleid: sql-concatenation-python
    cursor.execute(query)
    # ok: sql-concatenation-python
    cursor.execute("SELECT id FROM person WHERE name = ?", (name,))
    # ok: sql-concatenation-python
    cursor.execute("SELECT id FROM person " + "WHERE name = ?", (name,))
