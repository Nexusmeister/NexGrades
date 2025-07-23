using NexGrades.Domain.Models;

namespace NexGrades.Domain.Services.Interfaces;

public interface IGradesService
{
    Grade CalculateAllGrades(Student student);
    Grade CalculateGradeBySubject(Student student, Subject subject);
}