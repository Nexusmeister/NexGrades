﻿namespace NexGrades.Domain.Models;

public class Grade
{
    public decimal Value { get; set; }
    public uint Multiplier { get; set; }

    public Subject Subject { get; set; }
    public IList<Grade> SubGrades { get; set; }
}