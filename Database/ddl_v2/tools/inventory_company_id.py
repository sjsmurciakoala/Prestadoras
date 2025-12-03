#!/usr/bin/env python3
"""Escanea scripts SQL en ddl_v2 y lista tablas sin columna company_id."""
from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Iterable, List

MODULE_MAP = {
    "01_configuracion_base.sql": "Configuración",
    "02_contabilidad_core.sql": "Contabilidad",
    "03_contabilidad_catalogos.sql": "Contabilidad",
    "04_ventas_core.sql": "Ventas",
    "05_compras_core.sql": "Compras",
    "06_bancos_core.sql": "Bancos",
    "07_administracion_core.sql": "Administración",
    "08_inventarios_core.sql": "Inventarios",
    "09_activos_fijos_core.sql": "Activos fijos",
    "10_administracion_maestros.sql": "Administración",
    "11_administracion_transacciones.sql": "Administración",
    "12_contabilidad_configuracion_empresa.sql": "Contabilidad",
}

CREATE_PATTERN = re.compile(r"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([\w\.\"]+)", re.IGNORECASE)
COMPANY_PATTERN = re.compile(r"\bcompany_id\b", re.IGNORECASE)


@dataclass
class TableDefinition:
    script: str
    module: str
    table: str

    def to_markdown_row(self) -> str:
        return f"| {self.script} ({self.module}) | {self.table} |"


def iter_sql_files(base_path: Path) -> Iterable[Path]:
    for path in sorted(base_path.glob("*.sql")):
        yield path


def extract_tables(sql: str) -> List[tuple[str, str]]:
    tables: List[tuple[str, str]] = []
    for match in CREATE_PATTERN.finditer(sql):
        table_name = match.group(1).strip()
        paren_start = sql.find("(", match.end())
        if paren_start == -1:
            continue
        depth = 0
        end = None
        for idx in range(paren_start, len(sql)):
            char = sql[idx]
            if char == "(":
                depth += 1
            elif char == ")":
                depth -= 1
                if depth == 0:
                    end = idx
                    break
        if end is None:
            continue
        block = sql[paren_start + 1 : end]
        tables.append((table_name, block))
    return tables


def build_inventory(base_path: Path) -> List[TableDefinition]:
    inventory: List[TableDefinition] = []
    for sql_file in iter_sql_files(base_path):
        content = sql_file.read_text(encoding="utf-8", errors="ignore")
        for table_name, block in extract_tables(content):
            if not COMPANY_PATTERN.search(block):
                module = MODULE_MAP.get(sql_file.name, "Desconocido")
                inventory.append(
                    TableDefinition(
                        script=sql_file.name,
                        module=module,
                        table=table_name,
                    )
                )
    return inventory


def inventory_to_markdown(items: List[TableDefinition]) -> str:
    rows = "\n".join(item.to_markdown_row() for item in items)
    return "| Script (módulo) | Tabla |\n| --- | --- |\n" + rows


def inventory_to_json(items: List[TableDefinition]) -> str:
    return json.dumps([asdict(item) for item in items], indent=2, ensure_ascii=False)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--format",
        choices=("markdown", "json"),
        default="markdown",
        help="Formato de salida",
    )
    parser.add_argument(
        "--base",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Ruta de la carpeta ddl_v2",
    )
    args = parser.parse_args()

    inventory = build_inventory(args.base)
    if args.format == "markdown":
        print(inventory_to_markdown(inventory))
    else:
        print(inventory_to_json(inventory))


if __name__ == "__main__":
    main()
