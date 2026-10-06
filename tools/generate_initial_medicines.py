import os
import re

def parse_csv_line(line):
    result = []
    in_quotes = False
    current = []
    for ch in line:
        if ch == '"':
            in_quotes = not in_quotes
        elif ch == ',' and not in_quotes:
            result.append(''.join(current))
            current = []
        else:
            current.append(ch)
    result.append(''.join(current))
    return result

def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    md_path = os.path.join(root, 'data_stok_obat_untuk_launching.md')
    csv_path = os.path.join(root, 'Data', 'initial_medicines.csv')

    with open(md_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    table_rows = []
    for idx, line in enumerate(lines):
        line_str = line.strip()
        if line_str.startswith('|') and not line_str.startswith('|---') and ('Nama Obat' not in line_str):
            parts = [p.strip() for p in line_str.split('|')[1:-1]]
            if len(parts) >= 7:
                table_rows.append(parts)

    print(f'Total rows extracted from markdown: {len(table_rows)}')
    if len(table_rows) != 679:
        raise ValueError(f'Expected 679 rows, got {len(table_rows)}')

    output_lines = ['name,unit,purchase_price,sale_price,awl,msk,klr\n']

    for idx, r in enumerate(table_rows):
        no_str, name, unit, awl_str, sisa_str, hb_str, hj_str = r[:7]
        unit_val = '' if unit == '_(kosong)_' else unit
        awl = int(float(awl_str))
        sisa = int(float(sisa_str))
        hb = float(hb_str)
        hj = float(hj_str)

        if sisa > awl:
            msk = sisa - awl
            klr = 0
        else:
            msk = 0
            klr = awl - sisa

        # Check constraint: awl + msk - klr == sisa
        computed_sisa = awl + msk - klr
        assert computed_sisa == sisa, f'Mismatch at {name}: awl={awl}, msk={msk}, klr={klr}, computed={computed_sisa}, expected={sisa}'

        quoted_name = f'"{name}"' if ',' in name else name
        quoted_unit = f'"{unit_val}"' if ',' in unit_val else unit_val

        line = f'{quoted_name},{quoted_unit},{hb},{hj},{awl},{msk},{klr}\n'
        output_lines.append(line)

        # Validate with parse_csv_line
        parsed = parse_csv_line(line.strip())
        assert len(parsed) == 7, f'Error parsing line: {line.strip()}'

    with open(csv_path, 'w', encoding='utf-8') as f:
        f.writelines(output_lines)

    print(f'Successfully wrote {len(output_lines)-1} medicine records to {csv_path}')

if __name__ == '__main__':
    main()
