$(document).ready(function () {
    var $status = $("#Status");
    var $input = $("#roomName");

    $("#create").on("click", function () {
        var $packet = {
            roomName: $input.val()
        };
        $.ajax({
            type: "POST",
            url: "/api/createRoom",
            data: $packet,
            success: function (status) {
                $status.text(status);
            },
            error: function () {
                $status.text("Could not create room.");
            }
        });
    });
});