let objRef;

document.addEventListener("keydown", evt => {
    if(evt.key === '/') {
        // Disable quick find on Firefox
        evt.preventDefault();
    }
    objRef.invokeMethodAsync("JSKeyDown", evt.key);
});

function initObjectReference(ref) {
    objRef = ref;
}

function initFomanticElements() {
    $('input[type="checkbox"]').change(() => document.activeElement.blur());
    $('.ui.dropdown').dropdown({
        onHide: () => {document.activeElement.blur();}
    });
    $('#linesDropdown').dropdown({
        onHide: () => {document.activeElement.blur();},
        action: 'select'
    });
    $('#settingsButton').popup({
        inline: true,
        on: 'click'
    });
}

function regexRemoveMatches(regexString, source) {
    let re = new RegExp(regexString, 'g');
    return source.replaceAll(re, "");
}