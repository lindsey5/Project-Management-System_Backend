using System;

namespace ProjectAPI.Services;

public class ProjectService
{
    private readonly ApplicationDBContext _context;
    public ProjectService(ApplicationDBContext context){
        _context = context; 
    }
    public string GenerateUniqueProjectCode()
    {
        string code;
        bool isUnique = false;

        do
        {
            code = CodeGenerator.GenerateRandomCode(8);
            isUnique = !_context.Projects.Any(p => p.Project_code == code); 
        } while (!isUnique);

        return code;
    }

}
