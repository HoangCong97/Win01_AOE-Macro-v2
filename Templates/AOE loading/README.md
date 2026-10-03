# Dữ liệu xuất: AOE loading

- **Dự án**: AOE xoc quan
- **Thời gian xuất**: 2026-10-01 23:40:05
- **Tổng số vùng tọa độ (Areas)**: 1
- **Tổng số ảnh cắt (Crops)**: 10
- **Kèm ảnh gốc**: Không

## 1. Cấu trúc thư mục
```
AOE loading/
├── README.md        <- Tài liệu hướng dẫn & mô tả dữ liệu
├── data.json        <- Danh sách tọa độ và thông tin ảnh (sắp xếp theo Areas, Crops)
└── crops/           <- Thư mục chứa toàn bộ ảnh đã cắt (PNG)
```

## 2. Quy cách tệp data.json
Tệp `data.json` chứa thông tin chi tiết được phân nhóm rõ ràng theo 2 danh mục:
- **`areas`**: Danh sách các vùng tọa độ quan tâm (ROI - Region of Interest).
- **`crops`**: Danh sách các đối tượng đã được cắt ra tệp ảnh trong thư mục `crops/`.

### Hệ tọa độ:
- Gốc tọa độ `(0, 0)` nằm ở góc trên bên trái (Top-Left) của ảnh nguồn.
- `x`, `y`: Tọa độ góc trên bên trái của khung.
- `width`, `height`: Kích thước pixel chiều rộng và chiều cao.

## 3. Cách đọc dữ liệu bằng Python
```python
import json

with open('data.json', 'r', encoding='utf-8') as f:
    dataset = json.load(f)

print('Project:', dataset['project_name'])
print('Crops count:', len(dataset['crops']))
for crop in dataset['crops']:
    print(f"ID {crop['id']}: {crop['name']} -> {crop['relative_path']} ({crop['width']}x{crop['height']})")
```
