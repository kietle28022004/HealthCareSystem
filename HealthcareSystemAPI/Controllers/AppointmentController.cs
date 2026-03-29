using BusinessObjects.DataTransferObjects.AppointmentDTOs;
using BusinessObjects.DataTransferObjects.PaymentDTOs;
using BusinessObjects.Domain;
using Microsoft.AspNetCore.Mvc;
using Services.Interface;
using System.Linq;
using System.Security.Claims;

namespace HealthcareSystemAPI.Controllers
{

    [Route("api/[controller]")]
    [Controller]
    public class AppointmentController : Controller
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPaymentService _paymentService;

        public AppointmentController(IAppointmentService appointmentService, IPaymentService paymentService)
        {
            _appointmentService = appointmentService;
            _paymentService = paymentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var appointments = await _appointmentService.GetAll();
            return Ok(appointments);
        }

        [HttpGet("TimeOff/{id}")]
        public async Task<IActionResult> GetTimeOffAll(int id)
        {
            var timeOffs = await _appointmentService.GetTimeOffByDoctoridAsync(id);
            return Ok(timeOffs);
        }

        public class CreateTimeOffRequest
        {
            public int DoctorUserId { get; set; }
            public string Type { get; set; } = "personal";
            public string Title { get; set; } = string.Empty;
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
            public bool IsAllDay { get; set; } = true;
            public string? Reason { get; set; }
        }

        [HttpPost("TimeOff")]
        public async Task<IActionResult> CreateTimeOff([FromBody] CreateTimeOffRequest request)
        {
            if (request.DoctorUserId <= 0 || string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { message = "Invalid time off data" });
            }

            var created = await _appointmentService.CreateTimeOffAsync(new TimeOff
            {
                DoctorUserId = request.DoctorUserId,
                Type = request.Type,
                Title = request.Title,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsAllDay = request.IsAllDay,
                Reason = request.Reason
            });

            return Ok(created);
        }

        [HttpPut("TimeOff/{timeOffId}")]
        public async Task<IActionResult> UpdateTimeOff(int timeOffId, [FromBody] CreateTimeOffRequest request)
        {
            if (timeOffId <= 0 || request.DoctorUserId <= 0 || string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { message = "Invalid time off data" });
            }

            var updated = await _appointmentService.UpdateTimeOffAsync(timeOffId, new TimeOff
            {
                DoctorUserId = request.DoctorUserId,
                Type = request.Type,
                Title = request.Title,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsAllDay = request.IsAllDay,
                Reason = request.Reason
            });

            if (updated == null)
            {
                return NotFound(new { message = "TimeOff not found" });
            }

            return Ok(updated);
        }

        [HttpDelete("TimeOff/{timeOffId}")]
        public async Task<IActionResult> DeleteTimeOff(int timeOffId, [FromQuery] int doctorId)
        {
            if (timeOffId <= 0 || doctorId <= 0)
            {
                return BadRequest(new { message = "Invalid request" });
            }

            var deleted = await _appointmentService.DeleteTimeOffAsync(timeOffId, doctorId);
            if (!deleted)
            {
                return NotFound(new { message = "TimeOff not found" });
            }

            return NoContent();
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound(new { message = "Appointment not found" });

            return Ok(appointment);
        }


        [HttpGet("detail/{id}")]
        public async Task<IActionResult> GetAppointmentDetailAsync(int id)
        {
            var appointment = await _appointmentService.GetAppointmentForDoctorAsync(id);
            if (appointment == null)
                return NotFound(new { message = "Appointment not found" });

            return Ok(appointment);
        }


        [HttpGet("details")]
        public async Task<IActionResult> GetByDetails([FromQuery] int doctorId, [FromQuery] int patientId)
        {
            var appointment = await _appointmentService.GetByDetailsAsync(doctorId, patientId);
            if (appointment == null)
                return NotFound(new { message = "Appointment not found" });

            return Ok(appointment);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AppointmentAddRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.AppointmentDateTime <= DateTime.Now)
            {
                return BadRequest(new { message = "Appointment time must be in the future." });
            }

            var created = await _appointmentService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.AppointmentId }, created);
        }


        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AppoimentUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.AppointmentDateTime <= DateTime.Now)
            {
                return BadRequest(new { message = "Appointment time must be in the future." });
            }


            var updated = await _appointmentService.UpdateAsync(request, id);
            if (updated == null)
                return NotFound(new { message = "Appointment not found" });

            return Ok(updated);
        }


        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _appointmentService.DeleteAsync(id);
            if (!success)
                return NotFound(new { message = "Appointment not found or already deleted" });

            return NoContent();
        }


        [HttpPost("Pending")]
        public async Task<IActionResult> GetListPendingPatientId()
        {
            var userId = int.Parse(User.FindFirst("UserId").Value);
            var result = await _appointmentService.GetStatusPatientId("Pending", userId);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }

        [HttpPatch("Confirmed")]
        public async Task<IActionResult> RequestConfirm([FromBody] ConfirmRequest dto)
        {
            var result = await _appointmentService.RequestConfirm(dto);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }


        [HttpPatch("Completed")]
        public async Task<IActionResult> RequetsCompleted([FromBody] RejectRequest dto)
        {
            var result = await _appointmentService.RequestCompleted(dto);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }


        [HttpPost("Cancelled")]
        public async Task<IActionResult> GetListCancelledPatientId()
        {
            var userId = int.Parse(User.FindFirst("UserId").Value);
            var result = await _appointmentService.GetStatusPatientId("Cancelled", userId);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }

        [HttpGet("patient/{id}")]
        public async Task<IActionResult> GetAllPatientId(int id)
        {
            var result = await _appointmentService.GetByUserId(id);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }

        [HttpGet("specialty")]
        public async Task<IActionResult> GetAllspecialty()
        {
            var result = await _appointmentService.GetAllSpecialtiesAsync();
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }

        [HttpPost("timeslot3")]
        public async Task<IActionResult> IsTimeSlotBook3([FromBody] IsTimeSlotBook3Request dto)
        {
            if (dto.AppointmentDate <= DateTime.Now)
            {
                return Ok(true);
            }

            var result = await _appointmentService.IsTimeSlotBookedAsync(dto.DoctorId, dto.AppointmentDate, dto.excludeAppointmentId);
            return Ok(result);
        }
        [HttpPost("timeslot2")]
        public async Task<IActionResult> IsTimeSlotBook2([FromBody] IsTimeSlotBook2Request dto)
        {
            if (dto.AppointmentDate <= DateTime.Now)
            {
                return Ok(true);
            }

            var result = await _appointmentService.IsTimeSlotBookedAsync(dto.DoctorId, dto.AppointmentDate);
            return Ok(result);
        }


        [HttpGet("spe/{id}")]
        public async Task<IActionResult> GetDoctorBySpecicalty(int id)
        {
            var users = await _appointmentService.GetAllUsersAsync(id);
            return Ok(users ?? new List<DoctorSpecialtyResponse>());
        }

        [HttpGet("doctor/{id}")]
        public async Task<IActionResult> GetByDoctorId(int id)
        {
            var result = await _appointmentService.GetByDoctorId(id);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }

        [HttpPatch("Reject")]
        public async Task<IActionResult> RejectAppointment([FromBody] RejectRequest dto)
        {
            var result = await _appointmentService.UpdateRejectAsync(dto);
            if (result == null) return NotFound(new { message = "Appointment not found" });
            return Ok(result);
        }

        [HttpPost("{appointmentId}/payment")]
        public async Task<IActionResult> CreatePaymentForAppointment(int appointmentId, [FromBody] PaymentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Verify appointment exists
            var appointment = await _appointmentService.GetByIdAsync(appointmentId);
            if (appointment == null)
                return NotFound(new { message = "Appointment not found" });

            // Set appointment ID
            request.AppointmentId = appointmentId;

            try
            {
                var payment = await _paymentService.CreatePaymentAsync(request);
                return Ok(payment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating payment", error = ex.Message });
            }
        }

        

        [HttpPost("week")]

        public async Task<IActionResult> GetAppointmentsByWeek([FromBody] WeekAppointmentRequest dto)
        {
            var result = await _appointmentService.GetAppointmentsByWeekAsync(dto.DoctorId, dto.WeekStart);
            return Ok(result);
                    
        }


    }
}
