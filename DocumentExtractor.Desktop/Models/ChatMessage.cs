using System;
using Avalonia.Layout;

namespace DocumentExtractor.Desktop.Models;

/// <summary>
/// Represents a single chat message in the conversational learning interface.
/// Contains all information needed to display and manage chat interactions.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Name of the message sender (User, AI Assistant, System).
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// The actual message content/text.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of message for styling and behavior.
    /// </summary>
    public ChatMessageType Type { get; set; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Background color for the message bubble (for UI styling).
    /// </summary>
    public string BackgroundColor { get; set; } = "#F5F5F5";

    /// <summary>
    /// Horizontal alignment for the message bubble (Left for AI, Right for User).
    /// </summary>
    public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;

    /// <summary>
    /// Optional metadata for the message (confidence scores, suggestions, etc.).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Whether this message requires user verification or action.
    /// </summary>
    public bool RequiresAction { get; set; } = false;

    /// <summary>
    /// Session ID this message belongs to (for conversation tracking).
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Types of chat messages for different styling and behavior.
/// </summary>
public enum ChatMessageType
{
    /// <summary>
    /// Message from the user.
    /// </summary>
    User,

    /// <summary>
    /// Message from the AI assistant/bot.
    /// </summary>
    Bot,

    /// <summary>
    /// System message (errors, notifications, status updates).
    /// </summary>
    System,

    /// <summary>
    /// AI asking for verification of extracted data.
    /// </summary>
    Verification,

    /// <summary>
    /// AI providing learning feedback or pattern updates.
    /// </summary>
    Learning
}

/// <summary>
/// Represents a conversation session for tracking learning progress.
/// </summary>
public class ChatSession
{
    /// <summary>
    /// Unique identifier for the session.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of learning session.
    /// </summary>
    public SessionType Type { get; set; }

    /// <summary>
    /// When the session started.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Current status of the session.
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Active;

    /// <summary>
    /// Document being learned (if applicable).
    /// </summary>
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Template being learned (if applicable).
    /// </summary>
    public string? TemplatePath { get; set; }

    /// <summary>
    /// Learning progress/completeness.
    /// </summary>
    public double Progress { get; set; } = 0.0;

    /// <summary>
    /// Session notes or summary.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Types of learning sessions.
/// </summary>
public enum SessionType
{
    /// <summary>
    /// Learning to extract data from documents.
    /// </summary>
    DocumentLearning,

    /// <summary>
    /// Learning to fill templates with extracted data.
    /// </summary>
    TemplateLearning,

    /// <summary>
    /// Learning handwritten text patterns.
    /// </summary>
    HandwritingLearning,

    /// <summary>
    /// General conversation/help.
    /// </summary>
    General
}

/// <summary>
/// Status of a learning session.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Session is currently active.
    /// </summary>
    Active,

    /// <summary>
    /// Session completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Session was paused by user.
    /// </summary>
    Paused,

    /// <summary>
    /// Session was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Session encountered an error.
    /// </summary>
    Error
}