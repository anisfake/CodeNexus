# 📄 API: Admin Audit Logs

## `GET /api/admin/audit-logs`

> **Yêu cầu**: Role `Admin` + Bearer token trong header `Authorization`

---

## Query Parameters

| Param            | Type        | Default | Mô tả                                                       |
| ---------------- | ----------- | ------- | ------------------------------------------------------------ |
| `pageNumber`     | `int`       | `1`     | Trang hiện tại (≥ 1)                                        |
| `pageSize`       | `int`       | `10`    | Số bản ghi/trang (1–100)                                    |
| `action`         | `string?`   | `null`  | Lọc theo hành động: `Added`, `Modified`, `Deleted`           |
| `tableName`      | `string?`   | `null`  | Lọc theo tên bảng, ví dụ: `Users`, `Goals`, `LearningPaths` |
| `userId`         | `guid?`     | `null`  | Lọc theo user cụ thể                                        |
| `fromDate`       | `datetime?` | `null`  | Từ ngày (ISO 8601: `2025-01-01T00:00:00Z`)                  |
| `toDate`         | `datetime?` | `null`  | Đến ngày (`toDate` ≥ `fromDate`)                             |
| `sortBy`         | `enum`      | `0`     | `0` = Timestamp, `1` = Action, `2` = TableName              |
| `sortDescending` | `bool`      | `true`  | `true` = mới nhất trước                                     |

---

## Ví dụ Request

```http
GET /api/admin/audit-logs?pageNumber=1&pageSize=20&action=Modified&tableName=Users&sortBy=0&sortDescending=true
Authorization: Bearer <admin_token>
```

```http
GET /api/admin/audit-logs?userId=3fa85f64-5717-4562-b3fc-2c963f66afa6&fromDate=2025-06-01T00:00:00Z&toDate=2025-06-30T23:59:59Z
Authorization: Bearer <admin_token>
```

---

## Response `200 OK`

```json
{
  "items": [
    {
      "logId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "username": "john_doe",
      "action": "Modified",
      "tableName": "Users",
      "recordId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
      "oldValue": "{\"Status\":\"Active\"}",
      "newValue": "{\"Status\":\"Banned\"}",
      "timestamp": "2025-06-15T10:30:00Z",
      "ipAddress": "192.168.1.100"
    },
    {
      "logId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "userId": null,
      "username": null,
      "action": "Added",
      "tableName": "Users",
      "recordId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
      "oldValue": null,
      "newValue": "{\"Username\":\"new_user\",\"Email\":\"new@mail.com\"}",
      "timestamp": "2025-06-15T09:00:00Z",
      "ipAddress": "10.0.0.1"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 156,
  "totalPages": 8,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

## Response `400 Bad Request`

```json
{
  "errorCode": "INVALID_PAGE_SIZE",
  "errorMessage": "Page size must not exceed 100."
}
```

## Response `401 Unauthorized`

Không có token hoặc không phải Admin.

---

## TypeScript Interfaces

```typescript
// === Response types ===

interface AuditLogResponse {
  logId: string;
  userId: string | null;
  username: string | null;
  action: "Added" | "Modified" | "Deleted";
  tableName: string | null;
  recordId: string | null;
  oldValue: string | null; // JSON string, cần JSON.parse() để hiển thị chi tiết
  newValue: string | null; // JSON string
  timestamp: string; // ISO 8601
  ipAddress: string | null;
}

interface PaginatedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// === Query params ===

enum AuditLogSortBy {
  Timestamp = 0,
  Action = 1,
  TableName = 2,
}

interface GetAuditLogsParams {
  pageNumber?: number;
  pageSize?: number;
  action?: string;
  tableName?: string;
  userId?: string;
  fromDate?: string; // ISO 8601
  toDate?: string; // ISO 8601
  sortBy?: AuditLogSortBy;
  sortDescending?: boolean;
}

// === Usage ===

type AuditLogsResponse = PaginatedResult<AuditLogResponse>;
```

---

## Ghi chú

| Trường hợp          | `oldValue`                          | `newValue`                          |
| -------------------- | ----------------------------------- | ----------------------------------- |
| `action = "Added"`   | `null`                              | JSON chứa tất cả giá trị mới       |
| `action = "Deleted"` | JSON chứa tất cả giá trị trước xóa | `null`                              |
| `action = "Modified"`| JSON chứa giá trị cũ (chỉ field thay đổi) | JSON chứa giá trị mới (chỉ field thay đổi) |

- `oldValue` / `newValue` là **JSON string**. FE nên `JSON.parse()` rồi hiển thị dạng diff/table so sánh giá trị cũ ↔ mới.
- `userId = null` khi hành động xảy ra mà user chưa đăng nhập (ví dụ: register).
