using NexGrades.Domain.Models;
using NexGrades.Domain.Services.Interfaces;

namespace NexGrades.Domain.Services.Implementations;

public sealed class GradesService : IGradesService
{
    public Grade CalculateAllGrades(Student student)
    {
        throw new NotImplementedException();
    }

    public Grade CalculateGradeBySubject(Student student, Subject subject)
    {
        var relevantGrades = GetGradesFromStudent(student.Grades, subject).ToList();
        var calculatedGrade = CalculateSubGrade(relevantGrades);

        return new Grade
        {
            Multiplier = 1,
            SubGrades = relevantGrades,
            Subject = subject,
            Value = calculatedGrade
        };
    }


    private IEnumerable<Grade> GetGradesFromStudent(IEnumerable<Grade> gradesOfStudent, Subject subject)
    {
        var gradesToReturn = gradesOfStudent.Where(x => x.Subject.Equals(subject));
        return gradesToReturn;
    }

    private static decimal CalculateSubGrade(IList<Grade> subGrades)
    {
        var sum = 0.00M;
        foreach (var grade in subGrades)
        {
            if (grade.SubGrades.Any())
            {
                grade.Value = CalculateSubGrade(grade.SubGrades);
            }

            sum += grade.Value * grade.Multiplier;
        }

        return sum / subGrades.Count;
    }
}