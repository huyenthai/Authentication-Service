﻿namespace Authentication_Service.Events
{
    public class UserCreatedEvent
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}
