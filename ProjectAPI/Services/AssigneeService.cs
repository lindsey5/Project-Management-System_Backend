using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;

namespace ProjectAPI.Services;

public class AssigneeService
{   
    private readonly ApplicationDBContext _context;

    public AssigneeService(ApplicationDBContext applicationDBContext){
        _context = applicationDBContext;
    }

    public async Task<Assignee> CreateAssignee(Assignee assignee){
        if (assignee == null)
        {
            throw new ArgumentNullException(nameof(assignee));
        }

        _context.Assignees.Add(assignee);
        await _context.SaveChangesAsync();
        return assignee;
    }

    public async Task<List<AssigneeBaseDto>?> CreateAssignees(List<int> assigneesId, int task_Id, int project_Id){
        List<AssigneeBaseDto> Assignees = new List<AssigneeBaseDto>();

        if (assigneesId != null && assigneesId.Any())
        {
            foreach (var assigneeUserId in assigneesId.Distinct())
            {
                var member = await _context.Members.FirstOrDefaultAsync(m => m.User_Id == assigneeUserId && m.Project_Id == project_Id);

                if(member != null){
                    var assignee = new Assignee
                    {
                        Task_Id = task_Id,
                        Member_Id = assigneeUserId
                    };
                    var newAssignee = await CreateAssignee(assignee);
                    Assignees.Add(new AssigneeBaseDto{
                        Id = newAssignee.Id,
                        Member_Id = newAssignee.Member_Id,
                        Task_Id = newAssignee.Task_Id
                    });
                }
                
            }

            return Assignees;
        }else{
            return null;
        }
        
    }
}
