namespace CodeNexus.UnitTests;

using Xunit;
using FluentAssertions;

public class GamificationTests
{
    [Fact]
    public void CompleteTask_OnTime_ShouldAdd10XP()
    {
        // Chuẩn bị dữ liệu giả
        var user = new User { XP = 0 };
        var task = new TaskItem { Deadline = DateTime.Now.AddDays(1) }; // Chưa quá hạn

        // Test
        user.CompleteTask(task, finishTime: DateTime.Now);

        // Kiểm tra kết quả
        user.XP.Should().Be(10);
    }

    [Fact]
    public void CompleteTask_Late_ShouldAdd5XP()
    {
        var user = new User { XP = 0 };
        var task = new TaskItem { Deadline = DateTime.Now.AddDays(-1) }; // Đã quá hạn 1 ngày

        user.CompleteTask(task, finishTime: DateTime.Now);

        user.XP.Should().Be(5);
    }
}

class User
{
    public int XP { get; set; }
    public void CompleteTask(TaskItem task, DateTime finishTime)
    {
        if (finishTime <= task.Deadline)
        {
            XP += 10; // Hoàn thành đúng hạn
        }
        else
        {
            XP += 5; // Hoàn thành trễ hạn
        }
    }
}
class TaskItem
{
    public DateTime Deadline { get; set; }

}