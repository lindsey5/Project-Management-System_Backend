using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;

namespace ProjectAPI.Services;

public class AssigneeService
{   

    public async Task<List<Assignee>> CreateAssignees(ApplicationDBContext _context, List<int> assigneesMemberId, int task_Id, int project_Id)
    {
        var Assignees = new List<Assignee>();

        if (assigneesMemberId == null || !assigneesMemberId.Any())
            return Assignees;

        var distinctIds = assigneesMemberId.Distinct().ToList();

        try{
            var validMemberIds = await _context.Members
            .Where(m => distinctIds.Contains(m.Id) && m.Project_Id == project_Id)
            .Select(m => m.Id)
            .ToListAsync();

        foreach (var memberId in validMemberIds)
        {
            var newAssignee = new Assignee
            {
                Task_Id = task_Id,
                Member_Id = memberId
            };

            _context.Assignees.Add(newAssignee);
            await _context.SaveChangesAsync();

            Assignees.Add(new Assignee
            {
                Id = newAssignee.Id,
                Member_Id = newAssignee.Member_Id,
                Task_Id = newAssignee.Task_Id
            });
        }
        }catch(Exception ex){
            throw new Exception(ex.ToString());
        }


        return Assignees;
    }
}
