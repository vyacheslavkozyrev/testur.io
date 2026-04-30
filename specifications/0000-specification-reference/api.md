# API Contracts — [Feature Name] ([####])

## Error Response Format (RFC 7807 ProblemDetails)

All error responses use this shape:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "detail": "errors.validation.[field]"
}
```

The `detail` field always contains an **i18n key**, never hardcoded English text.

---

## [METHOD] /api/[resource]

**Auth**: Required
**Role**: [role or "Any authenticated user"]

### Request Body

```json
{
  "field_name": "string — description",
  "other_field": "integer — description"
}
```

**Validation Rules**

| Field | Required | Constraints |
|-------|----------|-------------|
| `field_name` | Yes | 1–200 characters |
| `other_field` | No | 1–100 (inclusive) |

### Response `200 OK`

```json
{
  "id": "uuid",
  "field_name": "string"
}
```

### Error Responses

| Status | `title` | `detail` |
|--------|---------|---------|
| 400 | Validation Failed | errors.validation.[field] |
| 401 | Unauthorized | errors.auth.required |
| 403 | Forbidden | errors.auth.forbidden |
| 404 | Not Found | errors.[resource].not_found |

---

## GET /api/[resource] — Paginated List

**Auth**: Required
**Role**: [role or "Any authenticated user"]

### Query Parameters

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-based) |
| `per_page` | integer | 20 | Items per page (max 100) |
| `sort_by` | string | `created_at` | Column to sort by |
| `sort_dir` | string | `desc` | `asc` or `desc` |
| `[filter]` | string | — | Optional filter param |

### Response `200 OK`

```json
{
  "data": [
    {
      "id": "uuid",
      "field_name": "string"
    }
  ],
  "pagination": {
    "page": 1,
    "per_page": 20,
    "total_items": 100,
    "total_pages": 5
  }
}
```

### Error Responses

| Status | `title` | `detail` |
|--------|---------|---------|
| 400 | Validation Failed | errors.validation.per_page_max |
| 401 | Unauthorized | errors.auth.required |
| 403 | Forbidden | errors.auth.forbidden |

---

## GET /api/[resource]/{id}

**Auth**: Required
**Role**: [role]

### Path Parameters

| Param | Type | Description |
|-------|------|-------------|
| `id` | uuid | Resource identifier |

### Response `200 OK`

```json
{
  "id": "uuid",
  "field_name": "string"
}
```

### Error Responses

| Status | `title` | `detail` |
|--------|---------|---------|
| 401 | Unauthorized | errors.auth.required |
| 403 | Forbidden | errors.auth.forbidden |
| 404 | Not Found | errors.[resource].not_found |

---

## PATCH /api/[resource]/{id}

**Auth**: Required
**Role**: [role]

### Path Parameters

| Param | Type | Description |
|-------|------|-------------|
| `id` | uuid | Resource identifier |

### Request Body

All fields optional — only provided fields are updated.

```json
{
  "field_name": "string — description"
}
```

**Validation Rules**

| Field | Required | Constraints |
|-------|----------|-------------|
| `field_name` | No | 1–200 characters if provided |

### Response `200 OK`

```json
{
  "id": "uuid",
  "field_name": "string"
}
```

### Error Responses

| Status | `title` | `detail` |
|--------|---------|---------|
| 400 | Validation Failed | errors.validation.[field] |
| 401 | Unauthorized | errors.auth.required |
| 403 | Forbidden | errors.auth.forbidden |
| 404 | Not Found | errors.[resource].not_found |

---

## DELETE /api/[resource]/{id}

**Auth**: Required
**Role**: [role]

Soft-deletes the resource (`is_deleted = true`). Data is never hard-deleted.

### Path Parameters

| Param | Type | Description |
|-------|------|-------------|
| `id` | uuid | Resource identifier |

### Response `204 No Content`

No response body.

### Error Responses

| Status | `title` | `detail` |
|--------|---------|---------|
| 401 | Unauthorized | errors.auth.required |
| 403 | Forbidden | errors.auth.forbidden |
| 404 | Not Found | errors.[resource].not_found |
