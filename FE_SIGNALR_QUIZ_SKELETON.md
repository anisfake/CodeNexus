# Tài liệu FE - Luồng sinh khung Quiz (`Quiz Skeleton`)

## 1) Cách FE gọi

Hiện tại FE gọi qua SignalR Hub `LessonHub` với method:

- `RequestLessonContent(lessonId: Guid)`

> Lưu ý: Chưa có method riêng để chỉ gen khung quiz. Việc gen quiz skeleton nằm trong cùng luồng gen lesson content.

---

## 2) Trình tự event SignalR FE sẽ nhận

Sau khi FE gọi `RequestLessonContent`, backend gửi theo thứ tự:

1. `LessonContentLoading`
2. `ReceiveLessonContent` hoặc `LessonContentError`
3. `QuizSkeletonLoading`
4. `ReceiveQuizSkeleton` hoặc `QuizSkeletonError`
5. `LessonGenerationCompleted`

---

## 3) Contract chi tiết cho phần Quiz Skeleton

## 3.1 Event loading

### Event: `QuizSkeletonLoading`

```json
{
  "lessonId": "guid"
}
```

## 3.2 Event thành công

### Event: `ReceiveQuizSkeleton`

```json
{
  "lessonId": "guid",
  "quizzes": [
    {
      "quizId": "guid",
      "title": "string",
      "description": "string | null",
      "timeLimit": "int | null",
      "passingScore": "decimal | null"
    }
  ]
}
```

### Mô tả field `quizzes[]`

- `quizId`: định danh quiz.
- `title`: tiêu đề quiz.
- `description`: mô tả quiz, có thể `null`.
- `timeLimit`: thời gian làm bài (phút), giai đoạn skeleton thường `null`.
- `passingScore`: điểm đạt, giai đoạn skeleton thường `null`.

> `quizzes` có thể là mảng rỗng `[]` và đây là trường hợp hợp lệ.

## 3.3 Event lỗi

### Event: `QuizSkeletonError`

```json
{
  "lessonId": "guid",
  "errorCode": "string",
  "errorMessage": "string"
}
```

---

## 4) Quy tắc backend hiện tại (để FE hiểu hành vi)

Trong `GenerateQuizSkeletonCommandHandler`:

- Nếu không tìm thấy lesson: trả lỗi `LESSON_NOT_FOUND`.
- Nếu user không có quyền lesson: trả lỗi `UNAUTHORIZED`.
- Nếu lesson đã có quiz: trả ngay danh sách quiz đã tồn tại.
- Nếu logic xác định lesson không cần quiz: trả `quizzes = []`.
- Nếu AI trả dữ liệu rỗng/không hợp lệ: `INVALID_AI_RESPONSE`.
- Nếu exception khác: `QUIZ_GENERATION_FAILED`.

---

## 5) Gợi ý xử lý FE

- Khi nhận `QuizSkeletonLoading`: hiện trạng thái loading cho block quiz.
- Khi nhận `ReceiveQuizSkeleton`:
  - Nếu `quizzes.length === 0`: hiển thị trạng thái “Bài này không có quiz”.
  - Nếu có phần tử: render danh sách quiz skeleton.
- Khi nhận `QuizSkeletonError`: hiển thị lỗi theo `errorMessage` (hoặc map theo `errorCode`).
- Không assume `timeLimit` và `passingScore` luôn có giá trị ở bước skeleton.

---

## 6) Nguồn tham chiếu code

- `src/CodeNexus.API/Hubs/LessonHub.cs`
- `src/CodeNexus.Application/Features/Quizzes/Commands/GenerateQuizSkeleton/GenerateQuizSkeletonCommandHandler.cs`
- `src/CodeNexus.Application/Features/Quizzes/DTOs/QuizSkeletonDto.cs`
