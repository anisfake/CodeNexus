using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Enums
{
    public enum NotificationType
    {
        Alert = 0,
        Message = 1,
        Reminder = 2,
        TaskOverdue = 3,
        ChapterOverdue = 4,
        LessonOverdue = 5,
        LearningPathOverdue = 6,
        ShareVersionUpdated = 7
    }
}
