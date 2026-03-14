# Hướng dẫn FE parse nội dung lesson được AI generate

Tài liệu này mô tả format output hiện tại từ backend (`GenerateLessonContentCommandHandler`) và cách parse để:
- Render từng section ổn định
- Hỗ trợ mentor edit từng section
- Ghép lại markdown đúng contract khi submit update

## 1) Output contract backend

Backend yêu cầu AI trả về đúng thứ tự section:

1. `## Overview`
2. `## Core Concepts`
3. `## Code Examples`
4. `## Common Mistakes`
5. `## Best Practices`
6. `## Summary`

Mỗi section có marker:

- `<!-- SECTION:overview:start --> ... <!-- SECTION:overview:end -->`
- `<!-- SECTION:core-concepts:start --> ... <!-- SECTION:core-concepts:end -->`
- `<!-- SECTION:code-examples:start --> ... <!-- SECTION:code-examples:end -->`
- `<!-- SECTION:common-mistakes:start --> ... <!-- SECTION:common-mistakes:end -->`
- `<!-- SECTION:best-practices:start --> ... <!-- SECTION:best-practices:end -->`
- `<!-- SECTION:summary:start --> ... <!-- SECTION:summary:end -->`

## 2) Định nghĩa dữ liệu FE đề xuất

```ts
export type LessonSectionKey =
  | 'overview'
  | 'core-concepts'
  | 'code-examples'
  | 'common-mistakes'
  | 'best-practices'
  | 'summary';

export interface ParsedLessonContent {
  sections: Record<LessonSectionKey, string>;
  raw: string;
}

export interface ParsedCommonMistake {
  title: string;
  wrongCode: string;
  correctCode: string;
  language?: string;
}
```

## 3) Parse section theo marker (khuyến nghị)

```ts
const SECTION_KEYS = [
  'overview',
  'core-concepts',
  'code-examples',
  'common-mistakes',
  'best-practices',
  'summary',
] as const;

type LessonSectionKey = (typeof SECTION_KEYS)[number];

function extractSection(markdown: string, key: LessonSectionKey): string {
  const start = `<!-- SECTION:${key}:start -->`;
  const end = `<!-- SECTION:${key}:end -->`;

  const startIndex = markdown.indexOf(start);
  const endIndex = markdown.indexOf(end);

  if (startIndex === -1 || endIndex === -1 || endIndex < startIndex) return '';

  return markdown
    .slice(startIndex + start.length, endIndex)
    .trim();
}

export function parseLessonContent(markdown: string) {
  const sections = {
    overview: extractSection(markdown, 'overview'),
    'core-concepts': extractSection(markdown, 'core-concepts'),
    'code-examples': extractSection(markdown, 'code-examples'),
    'common-mistakes': extractSection(markdown, 'common-mistakes'),
    'best-practices': extractSection(markdown, 'best-practices'),
    summary: extractSection(markdown, 'summary'),
  };

  return { sections, raw: markdown };
}
```

## 4) Parse `Common Mistakes` chi tiết

Template backend yêu cầu mỗi lỗi:

- `#### Mistake <number>: <short name>`
- `**Wrong**` + code fence
- `**Correct**` + code fence

Parser:

```ts
export function parseCommonMistakes(sectionMarkdown: string): ParsedCommonMistake[] {
  const pattern =
    /####\s*Mistake\s+\d+\s*:\s*(.+?)\s*\n\*\*Wrong\*\*\s*\n```([\w#+.-]*)\n([\s\S]*?)\n```\s*\n\*\*Correct\*\*\s*\n```[\w#+.-]*\n([\s\S]*?)\n```/g;

  const items: ParsedCommonMistake[] = [];
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(sectionMarkdown)) !== null) {
    items.push({
      title: match[1].trim(),
      language: match[2]?.trim() || undefined,
      wrongCode: match[3].trim(),
      correctCode: match[4].trim(),
    });
  }

  return items;
}
```

## 5) Render + edit flow cho mentor

### Bước 1: Parse
- Parse markdown lesson thành `sections`
- Parse riêng `sections['common-mistakes']` thành list mistakes

### Bước 2: Render form edit
- `overview`, `summary`: textarea markdown
- `core-concepts`, `best-practices`: markdown editor dạng list
- `code-examples`: markdown editor giữ nguyên code fence
- `common-mistakes`: form list item (`title`, `wrongCode`, `correctCode`)

### Bước 3: Build lại markdown khi save
- Ghép lại theo đúng thứ tự section contract
- Luôn giữ marker start/end
- Rebuild `Common Mistakes` theo đúng template

## 6) Hàm build markdown từ dữ liệu đã edit

```ts
type LessonSectionKey =
  | 'overview'
  | 'core-concepts'
  | 'code-examples'
  | 'common-mistakes'
  | 'best-practices'
  | 'summary';

const HEADING: Record<LessonSectionKey, string> = {
  overview: '## Overview',
  'core-concepts': '## Core Concepts',
  'code-examples': '## Code Examples',
  'common-mistakes': '## Common Mistakes',
  'best-practices': '## Best Practices',
  summary: '## Summary',
};

const ORDER: LessonSectionKey[] = [
  'overview',
  'core-concepts',
  'code-examples',
  'common-mistakes',
  'best-practices',
  'summary',
];

function wrapSection(key: LessonSectionKey, content: string) {
  return [
    `<!-- SECTION:${key}:start -->`,
    HEADING[key],
    content.trim(),
    `<!-- SECTION:${key}:end -->`,
  ].join('\n');
}

export function buildLessonMarkdown(sections: Record<LessonSectionKey, string>): string {
  return ORDER.map((key) => wrapSection(key, sections[key] ?? '')).join('\n\n');
}
```

## 7) Validation trước khi submit

Nên validate FE:
- Đủ 6 section key
- Có đủ marker start/end mỗi section
- `Common Mistakes` tối thiểu 1 item, tối đa 5 item
- Mỗi item có `title`, `wrongCode`, `correctCode`
- Code block không rỗng

## 8) Fallback khi AI output lệch format

Nếu thiếu marker:
1. fallback parse theo heading `## ...`
2. nếu vẫn fail, hiển thị raw markdown editor toàn bài
3. cho mentor sửa rồi save lại theo format chuẩn (rebuild bằng `buildLessonMarkdown`)

---

Nếu backend đổi contract, cập nhật lại tài liệu này và parser FE tương ứng.