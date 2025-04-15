using ProjectAPI.Models;

public class TaskResponseDto : TaskBaseDto
{
    public List<AssigneeBaseDto> Assignees { get; set; }
    public TaskResponseDto(ProjectAPI.Models.Task task, List<AssigneeBaseDto> Assignees)
    {
        Id = task.Id;
        Task_Name = task.Task_Name;
        Description = task.Description;
        Due_date = task.Due_date;
        Priority = task.Priority;
        Status = task.Status;
        this.Assignees = Assignees;
    }

}
